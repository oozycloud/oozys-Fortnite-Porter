(() => {
  'use strict';
  const entrance = document.querySelector('#entrance');
  const home = document.querySelector('#home');
  const begin = document.querySelector('#begin');
  const download = document.querySelector('#download');
  const status = document.querySelector('#release-status');
  const releasesUrl = 'https://github.com/oozycloud/oozys-Fortnite-Porter/releases';
  const apiUrl = 'https://api.github.com/repos/oozycloud/oozys-Fortnite-Porter/releases?per_page=100';
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  let entering = false;
  // Every fresh load begins at the entrance, including incoming homepage hashes.
  history.replaceState(null, '', location.pathname + location.search);
  begin.addEventListener('click', () => {
    if (entering) return;
    entering = true;
    entrance.classList.add('leaving');
    setTimeout(() => {
      entrance.hidden = true;
      home.hidden = false;
      document.dispatchEvent(new CustomEvent('porter:page', {detail: 'home'}));
      document.querySelector('#back-wordmark').focus({preventScroll: true});
      entering = false;
    }, reducedMotion.matches ? 0 : 650);
  });
  function back(event) {
    event.preventDefault();
    home.hidden = true;
    entrance.hidden = false;
    entrance.classList.remove('leaving');
    document.dispatchEvent(new CustomEvent('porter:page', {detail: 'entrance'}));
    begin.focus({preventScroll: true});
  }
  document.querySelector('#back').addEventListener('click', back);
  document.querySelector('#back-wordmark').addEventListener('click', back);

  function candidate(release) {
    if (release.draft || !release.published_at) return null;
    const match = `${release.tag_name || ''} ${release.name || ''}`.match(/\bbeta[\s_-]*v?(\d+)\.(\d+)\.(\d+)/i);
    if (!match) return null;
    const installer = (release.assets || []).find(asset => /installer.*\.exe$/i.test(asset.name) && /^https:\/\/github\.com\/oozycloud\/oozys-Fortnite-Porter\/releases\/download\//i.test(asset.browser_download_url || ''));
    return installer ? {version: match.slice(1).map(Number), label: `beta ${match.slice(1).join('.')}`, url: installer.browser_download_url} : null;
  }
  async function resolveRelease() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);
    try {
      const response = await fetch(apiUrl, {signal: controller.signal, cache: 'no-store', headers: {Accept: 'application/vnd.github+json'}});
      if (!response.ok) throw new Error('GitHub releases unavailable');
      const releases = await response.json();
      if (!Array.isArray(releases)) throw new Error('Unexpected release response');
      const choices = releases.map(candidate).filter(Boolean).sort((a,b) => b.version[0]-a.version[0] || b.version[1]-a.version[1] || b.version[2]-a.version[2]);
      if (!choices.length) throw new Error('No beta installer published');
      status.textContent = choices[0].label;
      return choices[0];
    } catch {
      status.replaceChildren();
      const fallback = document.createElement('a');
      fallback.href = releasesUrl;
      fallback.textContent = 'check releases on GitHub';
      status.append(fallback);
      return null;
    } finally { clearTimeout(timeout); }
  }
  download.addEventListener('click', async () => {
    download.disabled = true;
    status.textContent = 'checking latest installer…';
    const latest = await resolveRelease();
    download.disabled = false;
    if (latest) location.assign(latest.url);
    // A failed lookup stays on the page with an explicit Releases fallback.
  });
  resolveRelease();
})();
