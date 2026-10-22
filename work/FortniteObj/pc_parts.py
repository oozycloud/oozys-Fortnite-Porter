"""PCBS2 part-prefab catalog and static hierarchy export."""
import gc
import hashlib
import json
import math
from pathlib import Path

import UnityPy
from UnityPy.classes import PPtr
from UnityPy.helpers.MeshHelper import MeshHandler

_filters = json.loads(Path(__file__).with_name('pc_part_filters.json').read_text(encoding='utf-8'))
_excluded = {name.casefold() for name in _filters['exclude']}
_fittings = {name.casefold() for name in _filters['fittings']}


def category(path):
    pieces = path.replace("\\", "/").lower().split("/")
    if pieces[:3] != ["assets", "addressableassets", "prefabs"] or len(pieces) < 5:
        return None
    name = Path(path.replace('\\', '/')).stem.casefold()
    if name in _excluded:
        return None
    if name in _fittings:
        return 'Fittings'
    kind = pieces[3]
    if kind == "coolingsystems":
        if "fan" in pieces[4] or Path(path).stem.upper().startswith("FAN_"):
            return "Fans"
        return "Coolers" if pieces[4] in ("aircoolers", "liquidcoolers") else "Water cooling"
    return {"gpu": "GPUs", "motherboards": "Motherboards", "cpu": "CPUs", "cases": "Cases",
            "ram": "RAM", "hdd": "Storage", "ssd": "Storage", "psu": "Power supplies",
            "fans": "Fans", "radiators": "Radiators", "cables": "Cables", "screws": "Fittings"}.get(kind)


def write_json(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, allow_nan=False), encoding="utf-8")


def scan(request):
    root = Path(request["Root"])
    paths = sorted(root.rglob("*.bundle"))
    if not paths:
        raise ValueError("No PC Building Simulator 2 part bundles found. Choose the installed game's folder.")
    index_path = Path(request["IndexFile"])
    assets, warnings, index, seen = [], [], {}, set()
    for i, path in enumerate(paths):
        print(f"Reading Unity files {i + 1}/{len(paths)} · {path.name}", flush=True)
        try:
            env = UnityPy.load(str(path))
            for name in env.cabs:
                index[name.lower()] = str(path)
            for address, pointer in env.container.items():
                kind = category(address)
                if not kind or not address.lower().endswith(".prefab") or address.lower() in seen:
                    continue
                obj = pointer.deref()
                if obj.type.name != "GameObject":
                    continue
                seen.add(address.lower())
                identifier = hashlib.sha256(address.lower().encode()).hexdigest()[:20]
                assets.append(dict(Id=identifier, Name=Path(address).stem, Category=kind,
                    ObjectPath=json.dumps([obj.assets_file.name, obj.path_id, str(index_path)]),
                    PackagePath=str(path), Availability="Part model • static hierarchy"))
            del env
        except Exception as error:
            warnings.append(f"{path.name}: {error}")
        if i % 25 == 0:
            gc.collect()
    if not assets:
        raise ValueError("No supported PC part prefabs found. " + (warnings[0] if warnings else ""))
    index_path.parent.mkdir(parents=True, exist_ok=True)
    temporary = index_path.with_suffix(".tmp")
    write_json(temporary, index)
    temporary.replace(index_path)
    write_json(request["Result"], dict(Assets=assets, Warnings=warnings))


def export(request):
    asset = request["Asset"]
    source = json.loads(asset["ObjectPath"])
    if len(source) != 3:
        raise ValueError("This catalog is from an older version. Scan PC Building Simulator 2 again.")
    serialized, path_id, index_path = source
    index = json.loads(Path(index_path).read_text(encoding="utf-8"))
    env = UnityPy.load(asset["PackagePath"])
    original_find = env.find_file
    data_root = next((p for p in Path(asset["PackagePath"]).parents if p.name.lower().endswith("_data")), None)

    def find_file(name, is_dependency=True):
        key = name.replace("\\", "/").rsplit("/", 1)[-1].lower()
        loaded = env.get_cab(key)
        if loaded is not None:
            return loaded
        if key in index:
            env.load_file(index[key])
            loaded = env.get_cab(key)
            if loaded is not None:
                return loaded
        if data_root is not None:
            for candidate in (data_root / "Resources" / key, data_root / key):
                if candidate.is_file():
                    env.load_file(str(candidate), name=key)
                    loaded = env.get_cab(key)
                    if loaded is not None:
                        return loaded
        return original_find(name, is_dependency)
    env.find_file = find_file
    root = next((o for o in env.objects if o.path_id == path_id and o.assets_file.name == serialized), None)
    if root is None or root.type.name != "GameObject":
        raise ValueError("This part changed after the scan. Scan the game again.")
    output = Path(request["Output"])
    output.mkdir(parents=True, exist_ok=True)
    nodes, meshes, materials, images, warnings = [], {}, {}, {}, []
    material_overrides = {}

    def ref(owner, value):
        if not value or not value.get("m_PathID"):
            return None
        return PPtr(m_FileID=value["m_FileID"], m_PathID=value["m_PathID"], assetsfile=owner.assets_file).deref()

    def identity(obj):
        return obj.assets_file.name + ":" + str(obj.path_id)

    def material(reader):
        replaced = set()
        while identity(reader) in material_overrides:
            if identity(reader) in replaced:
                raise ValueError("Cyclic material replacement in part variant.")
            replaced.add(identity(reader))
            reader = material_overrides[identity(reader)]
        key = identity(reader)
        if key in materials:
            return key
        data = reader.parse_as_dict()
        saved = data.get("m_SavedProperties", {})
        colors = dict(saved.get("m_Colors", []))
        floats = dict(saved.get("m_Floats", []))
        color = colors.get("_BaseColor", colors.get("_Color", dict(r=0.8, g=0.8, b=0.8, a=1)))
        definition = dict(Name=data.get("m_Name", "Material"), Color=[color.get(c, 1) for c in "rgba"],
                          Metallic=floats.get("_Metallic", 0), Roughness=1-floats.get("_Smoothness", floats.get("_Glossiness", 0.5)))
        textures = dict(saved.get("m_TexEnvs", []))
        definition['Textures'] = {}
        definition['Floats'] = floats
        definition['Colors'] = colors
        if floats.get('_UVBase', 0) > 3 or floats.get('_UVEmissive', 0) > 3:
            warnings.append(f"{definition['Name']}: planar/triplanar shader coordinates use UV0 as an approximation.")
        for property_name, tex in textures.items():
            if not tex.get("m_Texture", {}).get("m_PathID"):
                continue
            try:
                texture = ref(reader, tex["m_Texture"])
                texture_id = identity(texture)
                if texture_id not in images:
                    filename = f"texture_{len(images):04d}.png"
                    image = texture.parse_as_object().image
                    limit = request.get('TextureSizeLimit', 0)
                    if limit and max(image.size) > limit:
                        image.thumbnail((limit, limit))
                    image.save(output / filename)
                    images[texture_id] = filename
                definition['Textures'][property_name] = dict(
                    Image=images[texture_id],
                    Scale=[tex.get("m_Scale", {}).get(c, 1) for c in "xy"],
                    Offset=[tex.get("m_Offset", {}).get(c, 0) for c in "xy"])
            except Exception as error:
                warnings.append(f"{definition['Name']} {property_name}: {error}")
        supported = {'_BaseColorMap', '_MainTex', '_BaseMap', '_NormalMap', '_BumpMap',
                     '_MaskMap', '_MetallicGlossMap', '_OcclusionMap', '_DetailMap',
                     '_EmissiveColorMap', '_EmissionMap'}
        for prop in definition['Textures'].keys() - supported:
            warnings.append(f"{definition['Name']} {prop}: texture preserved but shader input is not reconstructed.")
        materials[key] = definition
        return key

    def mesh(reader):
        key = identity(reader)
        if key in meshes:
            return key
        handler = MeshHandler(reader.parse_as_object())
        handler.process()
        if not handler.m_Vertices:
            warnings.append(f"{reader.peek_name() or 'Mesh'}: empty helper mesh skipped.")
            return None
        triangles, slots = [], []
        for slot, faces in enumerate(handler.get_triangles()):
            for a, b, c in faces:
                triangles.append([c, b, a])
                slots.append(slot)
        normals = handler.m_Normals or []
        if any(not math.isfinite(value) for normal in normals for value in normal):
            normals = []
            warnings.append(f"{reader.peek_name() or 'Mesh'}: invalid source normals will be recomputed from the faces.")
        meshes[key] = dict(Name=reader.peek_name() or "Mesh", Vertices=handler.m_Vertices,
                           UV=handler.m_UV0 or [], ExtraUV=[getattr(handler, f"m_UV{i}", None) or [] for i in range(1, 4)], Normals=normals, Faces=triangles, Slots=slots)
        return key

    visited = set()
    def visit(game_object, parent=None, active=True):
        key = identity(game_object)
        if key in visited:
            raise ValueError("Cyclic hierarchy in part prefab.")
        visited.add(key)
        data = game_object.parse_as_dict()
        components = []
        for entry in data.get("m_Component", []):
            pointer = entry.get("component") if isinstance(entry, dict) else entry[1]
            component = ref(game_object, pointer)
            if component is not None:
                components.append(component)
        transform = next((o for o in components if o.type.name == "Transform"), None)
        if transform is None:
            visited.remove(key)
            return
        tr = transform.parse_as_dict()
        node_index = len(nodes)
        active = active and bool(data.get("m_IsActive", True))
        node = dict(Name=data.get("m_Name", "Part"), Parent=parent,
                    Position=tr["m_LocalPosition"], Rotation=tr["m_LocalRotation"], Scale=tr["m_LocalScale"])
        renderer = next((o for o in components if o.type.name in ("MeshRenderer", "SkinnedMeshRenderer")), None)
        if renderer is not None and active:
            render = renderer.parse_as_dict()
            if render.get("m_Enabled", True):
                mesh_filter = next((o for o in components if o.type.name == "MeshFilter"), None)
                mesh_owner = mesh_filter or renderer
                mesh_data = mesh_owner.parse_as_dict()
                mesh_ref = ref(mesh_owner, mesh_data.get("m_Mesh"))
                if mesh_ref is not None:
                    mesh_key = mesh(mesh_ref)
                    if mesh_key is not None:
                        node["Mesh"] = mesh_key
                        node["Materials"] = []
                        for pointer in render.get("m_Materials", []):
                            try:
                                mat = ref(renderer, pointer)
                                node["Materials"].append(material(mat) if mat is not None else None)
                            except Exception as error:
                                node["Materials"].append(None)
                                warnings.append(f"{node['Name']} material: {error}")
        nodes.append(node)
        # Colour variants store a base prefab plus material substitutions instead
        # of copying its meshes. Read these fields as data; never run game scripts.
        for component in components:
            if component.type.name != "MonoBehaviour":
                continue
            definition = component.parse_as_dict()
            if ("m_source" not in definition or "m_dest" not in definition
                    or not definition.get("m_prefab", {}).get("m_PathID")):
                continue
            base = ref(component, definition["m_prefab"])
            if base is None or base.type.name != "GameObject":
                continue
            saved_overrides = material_overrides.copy()
            for before, after in zip(definition.get("m_source", []), definition.get("m_dest", [])):
                original, replacement = ref(component, before), ref(component, after)
                if original is not None and replacement is not None and original.type.name == replacement.type.name == "Material":
                    material_overrides[identity(original)] = replacement
            if definition.get("m_replaceComponent"):
                warnings.append(f"{node['Name']}: runtime component replacements are not reconstructed.")
            visit(base, node_index, active)
            material_overrides.clear()
            material_overrides.update(saved_overrides)
        for pointer in tr.get("m_Children", []):
            child_transform = ref(transform, pointer)
            child_object = ref(child_transform, child_transform.parse_as_dict()["m_GameObject"])
            visit(child_object, node_index, active)
        visited.remove(key)
    visit(root)
    count = sum("Mesh" in node for node in nodes)
    if not count:
        raise ValueError("This part has no enabled static meshes. Its geometry may be generated at runtime.")
    write_json(output / "part-scene.json", dict(Nodes=nodes, Meshes=meshes, Materials=materials))
    if materials:
        warnings.append("Unity materials use base colour maps and metallic/roughness values. Custom shaders, lighting and runtime effects are not fully reconstructed. Detail layers and ambient occlusion are approximations.")
    write_json(request["Result"], dict(Meshes=count, Textures=len(images), Warnings=warnings,
                                      Part=asset["Name"], Category=asset["Category"]))
