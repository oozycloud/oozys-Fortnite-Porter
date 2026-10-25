#include "Core.h"
#include "UnCore.h"
#include "UnrealPackage/UnPackage.h"
#include "GameDatabase.h"

#define MAKE_DIRS		1
//#define DISABLE_WRITE	1		// for quick testing of extraction

#define HOMEPAGE		"https://www.gildor.org/"


#if UNREAL4

int UE4UnversionedPackage(int verMin, int verMax)
{
	appErrorNoLog("Unversioned UE4 packages are not supported. Please restart UModel and select UE4 version in range %d-%d using UI or command line.", verMin, verMax);
	return -1;
}

bool UE4EncryptedPak()
{
	return false;
}

#endif // UNREAL4


/*-----------------------------------------------------------------------------
	Service functions
-----------------------------------------------------------------------------*/

static void GetFullExportFileName(const FObjectExport &Exp, UnPackage *Package, char *buf, int bufSize)
{
	// get full path
	Package->GetFullExportName(Exp, buf, bufSize);
	char *dst;
	// replace '.' with '/' and find the string end
	for (dst = buf; *dst; dst++)
		if (*dst == '.') *dst = '/';
	// and append class name as file extension
	assert(*dst == 0);
	const char *ClassName = Package->GetClassNameFor(Exp);
	appSprintf(dst, bufSize - (dst - buf), ".%s", ClassName);
}


static void GetFullExportName(const FObjectExport &Exp, UnPackage *Package, char *buf, int bufSize)
{
	// put class name
	const char *ClassName = Package->GetClassNameFor(Exp);
	appSprintf(buf, bufSize, "%s'", ClassName);
	// get full path
	char *dst = strchr(buf, 0);
	Package->GetFullExportName(Exp, dst, bufSize - (dst - buf));
	// append "'"
	dst = strchr(buf, 0);
	appSprintf(dst, bufSize - (dst - buf), "'");
}


static TArray<FString> filters;

static bool FilterClass(const char *ClassName)	//?? check logic: filter = pass or reject
{
	if (!filters.Num()) return true;

	bool filter = false;
	for (int fidx = 0; fidx < filters.Num(); fidx++)
	{
		if (!stricmp(*filters[fidx], ClassName) || (filters[fidx].Len()>0 && (*filters[fidx])[filters[fidx].Len()-1]=='*' && !strnicmp(*filters[fidx],ClassName,filters[fidx].Len()-1)))
			return true;
	}
	return false;
}

// Attempts to evaluate the provided Key as hex string, does inplace replacement
static void CheckHexAesKey(FString& Key)
{
#define ishex(c)		( (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') )
#define hextodigit(c)	( (c >= 'a') ? c - 'a' + 10 : c - '0' )

	if (Key.Len() < 3) return;
	const char* s = *Key;

	// Hex key starts with "0x"
	if (*s++ != '0') return;
	if (tolower(*s++) != 'x') return;

	FStaticString<256> NewKey;
	NewKey.Empty(Key.Len() / 2 + 1);

	int remains = Key.Len() - 2;
	if (remains & 1)
	{
		appErrorNoLog("Hexadecimal AES key contains odd number of characters");
	}
	while (remains > 0)
	{
		uint8 b = 0;
		if ((remains & 1) == 0)
		{
			// this code will not be executed only in a case of odd character count, for the first char
			char c = tolower(*s++);
			if (!ishex(c))
			{
				appErrorNoLog("Illegal character in hexadecimal AES key");
			}
			b = hextodigit(c) << 4;
			remains--;
		}
		char c = tolower(*s++);
		if (!ishex(c))
		{
			appErrorNoLog("Illegal character in hexadecimal AES key");
		}
		b |= hextodigit(c);
		remains--;

		NewKey.AppendChar((char)b);
	}

	Key = NewKey;
}

static void HandleAesKeyOption(const char* value)
{
	FStaticString<256> Key = value;
	if (Key.Len())
	{
		if (Key[0] == '@')
		{
			// Load a file with keys
			const char* KeyFile = *Key + 1;
			FILE* f = fopen(KeyFile, "r");
			if (f)
			{
				char buffer[1024];
				while (!feof(f))
				{
					if (fgets(buffer, ARRAY_COUNT(buffer), f))
					{
						FStaticString<256> Key = buffer;
						Key.TrimStartAndEndInline();
						if (!Key.IsEmpty())
						{
							CheckHexAesKey(Key);
							GAesKeys.Add(Key);
						}
					}
				}
				fclose(f);
			}
			else
			{
				appPrintf("Warning: -aes option refers missing file %s\n", KeyFile);
			}
		}
		else
		{
			Key.TrimStartAndEndInline();
			CheckHexAesKey(Key);
			GAesKeys.Add(Key);
		}
	}
}


/*-----------------------------------------------------------------------------
	Main function
-----------------------------------------------------------------------------*/

int main(int argc, char **argv)
{
#if DO_GUARD
	TRY {
#endif

	guard(Main);

	// display usage
	if (argc < 2)
	{
	help:
		printf(	"Unreal Engine package extractor\n"
				"Usage: extract [command] [options] <package filename>\n"
				"\n"
				"Commands:\n"
				"    -extract        (default) extract package\n"
				"    -list           list contents of package\n"
				"\n"
				"Options:\n"
				"    -path=PATH      path to game installation directory; if not specified,\n"
				"                    program will search for packages in current directory\n"
				"    -game=tag       override game autodetection (see -taglist for variants)\n"
				"    -aes=key        provide AES decryption key for encrypted pak files,\n"
				"                    key is ASCII or hex string (hex format is 0xAABBCCDD)\n"
				"    -filter=<value> add filter for output types\n"
				"    -out=PATH       extract everything into PATH instead of the current directory\n"
				"    -lzo|lzx|zlib   force compression method for fully-compressed packages\n"
				"    -log=file       write log to the specified file\n"
				"    -taglist        list of tags to override game autodetection\n"
				"    -help           display this help page\n"
				"\n"
				"Platform selection:\n"
				"    -ps3               override platform autodetection to PS3\n"
				"\n"
				"For details and updates please visit " HOMEPAGE "\n"
		);
		exit(0);
	}

	// parse command line
	enum
	{
		CMD_Extract,
		CMD_List
	};

	static byte mainCmd = CMD_Extract;
	bool hasRootDir = false;
	char BaseDir[256];
	strcpy(BaseDir, ".");

	const char *argPkgName = NULL;

	int arg;
	for (arg = 1; arg < argc; arg++)
	{
		const char *opt = argv[arg];
		if (opt[0] != '-')
		{
			if (argPkgName)
			{
				appPrintf("Package name already specified (passed \"%s\" and \"%s\")\n", argPkgName, opt);
				return 1;
			}
			argPkgName = opt;
			continue;
		}

		opt++;			// skip '-'

		if (!stricmp(opt, "extract"))
			mainCmd = CMD_Extract;
		else if (!stricmp(opt, "list"))
			mainCmd = CMD_List;
		else if (!strnicmp(opt, "filter=", 7))
		{
			FString* S = new (filters) FString(opt+7);
		}
		else if (!strnicmp(opt, "log=", 4))
		{
			appOpenLogFile(opt+4);
		}
		else if (!strnicmp(opt, "path=", 5))
		{
			appSetRootDirectory(opt+5);
			hasRootDir = true;
		}
		else if (!strnicmp(opt, "out=", 4))
		{
			strcpy(BaseDir, opt+4);
		}
		else if (!strnicmp(opt, "game=", 5))
		{
			int tag = FindGameTag(opt+5);
			if (tag == -1)
			{
				appPrintf("ERROR: unknown game tag \"%s\". Use -taglist option to display available tags.\n", opt+5);
				exit(0);
			}
			GForceGame = tag;
		}
		else if (!strnicmp(opt, "aes=", 4))
		{
			HandleAesKeyOption(opt+4);
		}
		else if (!stricmp(opt, "lzo"))
			GForceCompMethod = COMPRESS_LZO;
		else if (!stricmp(opt, "zlib"))
			GForceCompMethod = COMPRESS_ZLIB;
		else if (!stricmp(opt, "lzx"))
			GForceCompMethod = COMPRESS_LZX;
		else if (!stricmp(opt, "ps3"))
			GForcePlatform = PLATFORM_PS3;
		else if (!stricmp(opt, "taglist"))
		{
			PrintGameList(true);
			return 0;
		}
		else if (!stricmp(opt, "help"))
		{
			goto help;
		}
		else
		{
			appPrintf("decompress: invalid option: %s\n", opt);
			return 1;
		}
	}
	if (!argPkgName) goto help;

	if (!hasRootDir)
		appSetRootDirectory2(argPkgName);

	// setup NotifyInfo to describe package only
	appSetNotifyHeader(argPkgName);
	// load a package
	UnPackage *Package = UnPackage::LoadPackage(argPkgName);
	if (!Package)
	{
		printf("ERROR: Unable to find/load package %s\n", argPkgName);
		exit(1);
	}
	// prepare package for reading
	Package->Open();

	guard(ProcessPackage);

    // Complete object references are needed even when raw payloads are class-filtered.
    char fullTableFile[2048];
    appSprintf(ARRAY_ARG(fullTableFile), "%s/%s/Objects.txt", BaseDir, Package->Name);
    appMakeDirectoryForFile(fullTableFile);
    FILE* objectTable=fopen(fullTableFile,"w");
    if(!objectTable) appError("Could not create object metadata");
    for(int objectIndex=0;objectIndex<Package->Summary.ExportCount;objectIndex++) {
        char objectName[2048];GetFullExportName(Package->ExportTable[objectIndex],Package,ARRAY_ARG(objectName));
        fprintf(objectTable,"%d = %s\n",objectIndex,objectName);
    }
    fclose(objectTable);
      appSprintf(ARRAY_ARG(fullTableFile), "%s/%s/ObjectLinks.txt", BaseDir, Package->Name);
      objectTable=fopen(fullTableFile,"w");
      if(!objectTable) appError("Could not create object links");
      for(int objectIndex=0;objectIndex<Package->Summary.ExportCount;objectIndex++) {
          const FObjectExport& e=Package->ExportTable[objectIndex];
          fprintf(objectTable,"%d\t%d\t%d\t%u\n",objectIndex+1,e.PackageIndex,e.Archetype,e.ObjectFlags);
      }
      for(int objectIndex=0;objectIndex<Package->Summary.ImportCount;objectIndex++) {
          const FObjectImport& e=Package->ImportTable[objectIndex];
          fprintf(objectTable,"%d\t%d\t0\t0\n",-objectIndex-1,e.PackageIndex);
      }
      fclose(objectTable);
	int idx;

	if (mainCmd == CMD_List)
	{
		for (idx = 0; idx < Package->Summary.ExportCount; idx++)
		{
			FObjectExport &Exp = Package->ExportTable[idx];
			const char *ClassName = Package->GetClassNameFor(Exp);
			if (!FilterClass(ClassName)) continue;
			char objName[2048];
			GetFullExportName(Exp, Package, ARRAY_ARG(objName));
			printf("%5d  %s\n", idx, objName);
		}
		return 0;
	}

	// extract package name, create directory for it
	char PkgName[256];
	const char *s = strrchr(argPkgName, '/');
	if (!s) s = strrchr(argPkgName, '\\');			// WARNING: not processing mixed '/' and '\'
	if (s) s++; else s = argPkgName;
	appStrncpyz(PkgName, s, ARRAY_COUNT(PkgName));
	char *s2 = strchr(PkgName, '.');
	if (s2) *s2 = 0;
	// extract objects and write export table
	FILE *f;
	char buf2[2048];
	guard(ExtractObjects);
	appSprintf(ARRAY_ARG(buf2), "%s/%s/ExportTable.txt", BaseDir, PkgName);
	appMakeDirectoryForFile(buf2);
	f = fopen(buf2, "w");
	assert(f);
	for (idx = 0; idx < Package->Summary.ExportCount; idx++)
	{
		FObjectExport &Exp = Package->ExportTable[idx];
		const char *ClassName = Package->GetClassNameFor(Exp);
		if (!FilterClass(ClassName)) continue;
		// prepare file
#if !MAKE_DIRS
		char buf3[1024];
		buf3[0] = 0;
		if (Exp.PackageIndex) //?? GetObjectName() will return "Class" for index=0 ...
		{
			const char *Outer = Package->GetObjectName(Exp.PackageIndex);
			appSprintf(ARRAY_ARG(buf3), "%s.", Outer);
		}
		fprintf(f, "%d = %s'%s%s'\n", idx, ClassName, buf3, *Exp.ObjectName);
		appSprintf(ARRAY_ARG(buf2), "%s/%s/%s%s.%s", BaseDir, buf, buf3, *Exp.ObjectName, ClassName);
#else
		char objName[2048];
		GetFullExportName(Exp, Package, ARRAY_ARG(objName));
		fprintf(f, "%d = %s\n", idx, objName);
		// Flat numeric names avoid MAX_PATH failures on deeply nested archetypes.
		appSprintf(ARRAY_ARG(buf2), "%s/%s/%d.%s", BaseDir, PkgName, idx+1, ClassName);
#endif
		guard(WriteFile);
		// read data
		byte *data = new byte[Exp.SerialSize];
		if (Exp.SerialSize)
		{
		#if 0
			Package->Seek(Exp.SerialOffset);
		#else
			Package->SetupReader(idx);
		#endif
			Package->Serialize(data, Exp.SerialSize);
		}
#if !DISABLE_WRITE
		appMakeDirectoryForFile(buf2);
		FILE *f2 = fopen(buf2, "wb");
		if (!f2)
		{
			//!! note: cannot create file with name "con" (any extension)
			printf("%d/%d: unable to create file %s\n", idx, Package->Summary.ExportCount, buf2);
			continue;
		}
		// write data
		fwrite(data, Exp.SerialSize, 1, f2);
		fclose(f2);
#endif // !DISABLE_WRITE
		// cleanup
		delete data;
		unguardf("file=%s", buf2);
		// notification
		printf("Done: %d/%d ...\r", idx, Package->Summary.ExportCount);
	}
	fclose(f);
	printf("Done ...             \n");
	unguard;
	// write name table
	guard(WriteNameTable);
	appSprintf(ARRAY_ARG(buf2), "%s/%s/NameTable.txt", BaseDir, PkgName);
	f = fopen(buf2, "w");
	assert(f);
	for (idx = 0; idx < Package->Summary.NameCount; idx++)
		fprintf(f, "%d = \"%s\"\n", idx, Package->NameTable[idx]);
	fclose(f);
	unguard;
	// write import table
	guard(WriteImportTable);
	appSprintf(ARRAY_ARG(buf2), "%s/%s/ImportTable.txt", BaseDir, PkgName);
	f = fopen(buf2, "w");
	assert(f);
	for (idx = 0; idx < Package->Summary.ImportCount; idx++)
	{
		const FObjectImport &Imp = Package->GetImport(idx);
		const char *PackageName = Package->GetObjectPackageName(Imp.PackageIndex);
		if (PackageName)
			fprintf(f, "%d = %s'%s.%s'\n", idx, *Imp.ClassName, PackageName, *Imp.ObjectName);
		else
			fprintf(f, "%d = %s'%s'\n", idx, *Imp.ClassName, *Imp.ObjectName);
	}
	fclose(f);
	unguard;

	unguard;

	unguard;

#if DO_GUARD
	} CATCH {
		GError.StandardHandler();
		exit(1);
	}
#endif
	return 0;
}
