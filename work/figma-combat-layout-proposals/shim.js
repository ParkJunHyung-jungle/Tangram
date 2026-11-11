// 미리보기 전용: code.js가 쓰는 Figma Plugin API 일부를 DOM으로 흉내 내어 렌더링한다.
// URL 매개변수: cmd=layouts|kits, v=A|B|C(레이아웃 화면), node=<노드 이름>, scale=<배율>
(function () {
  const params = new URLSearchParams(location.search);
  const FAMILIES = ['Noto Sans KR', 'Noto Serif KR', 'Gowun Batang', 'IBM Plex Sans KR'];
  const measureBox = document.createElement('div');
  measureBox.style.cssText = 'position:absolute;left:-99999px;top:0;visibility:hidden';
  document.body.appendChild(measureBox);

  function textStyle(n) {
    const lh = n.lineHeight && n.lineHeight.value ? n.lineHeight.value + 'px' : 'normal';
    return `font-family:'${n.fontName.family}','Noto Sans KR','Malgun Gothic',sans-serif;font-size:${n.fontSize}px;line-height:${lh};font-weight:${n.fontName.style === 'Bold' ? 700 : 400};`;
  }
  function measure(n, fixedWidth) {
    const d = document.createElement('div');
    d.style.cssText = textStyle(n) + (fixedWidth ? `width:${fixedWidth}px;white-space:pre-wrap;word-break:keep-all;overflow-wrap:anywhere;` : 'white-space:pre;display:inline-block;');
    d.textContent = n.characters;
    measureBox.appendChild(d);
    const r = { w: d.offsetWidth, h: d.offsetHeight };
    measureBox.removeChild(d);
    return r;
  }

  let counter = 0;
  class Node {
    constructor(type) {
      this.type = type; this.id = 'n' + (++counter); this.children = []; this.parent = null;
      this.name = ''; this.x = 0; this.y = 0; this._w = 100; this._h = 100;
      this.fills = []; this.strokes = []; this.strokeWeight = 1; this.cornerRadius = 0; this.opacity = 1;
      this.effects = []; this.clipsContent = type === 'FRAME'; this.dashPattern = [];
      if (type === 'TEXT') { this.fontName = { family: 'Inter', style: 'Regular' }; this.fontSize = 12; this.characters = ''; this.textAutoResize = 'WIDTH_AND_HEIGHT'; this.textAlignHorizontal = 'LEFT'; this.textAlignVertical = 'TOP'; this.lineHeight = null; }
    }
    get width() {
      if (this.type === 'TEXT' && this.textAutoResize === 'WIDTH_AND_HEIGHT') return measure(this).w;
      return this._w;
    }
    get height() {
      if (this.type === 'TEXT' && this.textAutoResize === 'WIDTH_AND_HEIGHT') return measure(this).h;
      if (this.type === 'TEXT' && this.textAutoResize === 'HEIGHT') return measure(this, this._w).h;
      return this._h;
    }
    resize(w, h) { this._w = w; this._h = h; }
    resizeWithoutConstraints(w, h) { this.resize(w, h); }
    appendChild(c) { if (c.parent) c.remove(); c.parent = this; this.children.push(c); }
    remove() { if (this.parent) this.parent.children = this.parent.children.filter(c => c !== this); this.parent = null; }
    findOne(pred) { for (const c of this.children) { if (pred(c)) return c; const r = c.findOne(pred); if (r) return r; } return null; }
    createInstance() {
      if (this.type !== 'COMPONENT') throw new Error('createInstance는 컴포넌트에서만 호출할 수 있습니다');
      const inst = clone(this); inst.type = 'INSTANCE'; inst.mainComponent = this; return inst;
    }
  }
  function clone(n) {
    const c = new Node(n.type);
    for (const k of Object.keys(n)) if (!['id', 'parent', 'children', 'mainComponent'].includes(k)) c[k] = Array.isArray(n[k]) ? n[k].slice() : (n[k] && typeof n[k] === 'object' ? { ...n[k] } : n[k]);
    for (const ch of n.children) c.appendChild(clone(ch));
    return c;
  }

  const page = new Node('PAGE');
  page.name = '페이지 (미리보기)';
  const root = new Node('FRAME');
  root.name = 'Frame 34 (미리보기)'; root.resize(2000, 2000);
  page.appendChild(root);

  window.figma = {
    command: params.get('cmd') || 'layouts',
    currentPage: page,
    createFrame: () => new Node('FRAME'),
    createRectangle: () => new Node('RECTANGLE'),
    createEllipse: () => new Node('ELLIPSE'),
    createVector: () => new Node('VECTOR'),
    createText: () => new Node('TEXT'),
    createComponent: () => new Node('COMPONENT'),
    combineAsVariants(nodes, parent) { const set = new Node('COMPONENT_SET'); parent.appendChild(set); for (const n of nodes) set.appendChild(n); return set; },
    getNodeByIdAsync: async () => root,
    setCurrentPageAsync: async () => {},
    loadFontAsync: async () => {},
    listAvailableFontsAsync: async () => FAMILIES.flatMap(f => ['Regular', 'Bold'].map(s => ({ fontName: { family: f, style: s } }))),
    viewport: { scrollAndZoomIntoView() {} },
    closePlugin: msg => { window.__closeMessage = msg; render(); },
  };

  function rgba(p) {
    const c = p.color, a = p.opacity ?? 1;
    return `rgba(${Math.round(c.r * 255)},${Math.round(c.g * 255)},${Math.round(c.b * 255)},${a})`;
  }
  function draw(n, parentEl) {
    let el;
    if (n.type === 'VECTOR') {
      el = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      el.setAttribute('width', 1); el.setAttribute('height', 1);
      el.style.cssText = `position:absolute;left:${n.x}px;top:${n.y}px;overflow:visible;opacity:${n.opacity}`;
      const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      path.setAttribute('d', n.vectorPaths[0].data);
      path.setAttribute('fill', n.fills.length ? rgba(n.fills[0]) : 'none');
      if (n.strokes.length) {
        path.setAttribute('stroke', rgba(n.strokes[0])); path.setAttribute('stroke-width', n.strokeWeight);
        path.setAttribute('stroke-linecap', (n.strokeCap || 'NONE') === 'ROUND' ? 'round' : 'butt'); path.setAttribute('stroke-linejoin', 'round');
        if (n.dashPattern && n.dashPattern.length) path.setAttribute('stroke-dasharray', n.dashPattern.join(' '));
      }
      el.appendChild(path);
      parentEl.appendChild(el);
      return;
    }
    el = document.createElement('div');
    let css = `position:absolute;box-sizing:border-box;left:${n.x}px;top:${n.y}px;opacity:${n.opacity};`;
    if (n.type === 'TEXT') {
      css += textStyle(n) + `color:${n.fills.length ? rgba(n.fills[0]) : '#000'};text-align:${n.textAlignHorizontal.toLowerCase()};`;
      if (n.textAutoResize === 'WIDTH_AND_HEIGHT') css += 'white-space:pre;';
      else css += `width:${n._w}px;white-space:pre-wrap;word-break:keep-all;overflow-wrap:anywhere;`;
      if (n.textAutoResize === 'NONE') {
        const v = { TOP: 'flex-start', CENTER: 'center', BOTTOM: 'flex-end' }[n.textAlignVertical];
        const h = { LEFT: 'flex-start', CENTER: 'center', RIGHT: 'flex-end' }[n.textAlignHorizontal];
        css += `height:${n._h}px;display:flex;align-items:${v};justify-content:${h};`;
      }
      el.style.cssText = css;
      el.textContent = n.characters;
      parentEl.appendChild(el);
      return;
    }
    css += `width:${n._w}px;height:${n._h}px;`;
    if (n.fills.length) css += `background:${rgba(n.fills[n.fills.length - 1])};`;
    if (n.type === 'ELLIPSE') css += 'border-radius:50%;'; else if (n.cornerRadius) css += `border-radius:${n.cornerRadius}px;`;
    if (n.strokes.length) css += `border:${n.strokeWeight}px ${n.dashPattern && n.dashPattern.length ? 'dashed' : 'solid'} ${rgba(n.strokes[0])};`;
    if (n.clipsContent) css += 'overflow:hidden;';
    const sh = (n.effects || []).find(e => e.type === 'DROP_SHADOW');
    if (sh) css += `box-shadow:${sh.offset.x}px ${sh.offset.y}px ${sh.radius}px rgba(0,0,0,${sh.color.a});`;
    el.style.cssText = css;
    el.title = n.name;
    parentEl.appendChild(el);
    // 테두리 두께만큼 자식이 밀리지 않도록 내부 컨테이너를 테두리 바깥 기준으로 둔다.
    const inner = document.createElement('div');
    inner.style.cssText = `position:absolute;left:${-(n.strokes.length ? n.strokeWeight : 0)}px;top:${-(n.strokes.length ? n.strokeWeight : 0)}px;`;
    el.appendChild(inner);
    for (const c of n.children) draw(c, inner);
  }
  function find(n, pred) { if (pred(n)) return n; for (const c of n.children) { const r = find(c, pred); if (r) return r; } return null; }

  function render() {
    const stage = document.getElementById('stage');
    stage.innerHTML = '';
    if (window.__closeMessage && window.__closeMessage.startsWith('오류')) { stage.textContent = window.__closeMessage; stage.style.color = '#fff'; stage.style.fontSize = '32px'; document.title = 'error'; return; }
    const v = params.get('v'), name = params.get('node');
    let target;
    if (v) target = find(page, n => n.name === `화면 ${v} · 1920×1080`);
    else if (name) target = find(page, n => n.name === name);
    else target = page.children[page.children.length - 1].type === 'FRAME' && figma.command === 'kits' ? page.children[page.children.length - 1] : root.children[0];
    if (!target) { stage.textContent = '노드를 찾지 못했습니다: ' + (v || name); stage.style.color = '#fff'; return; }
    const copy = Object.assign(Object.create(Object.getPrototypeOf(target)), target);
    copy.x = 0; copy.y = 0;
    const scale = parseFloat(params.get('scale') || '1');
    stage.style.transform = `scale(${scale})`; stage.style.transformOrigin = '0 0';
    draw(copy, stage);
    document.title = 'done';
  }

  Promise.all(FAMILIES.flatMap(f => [document.fonts.load(`400 16px "${f}"`), document.fonts.load(`700 16px "${f}"`)])).catch(() => {}).then(() => {
    const s = document.createElement('script');
    s.src = 'code.js';
    document.body.appendChild(s);
  });
})();
