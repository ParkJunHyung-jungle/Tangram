// 주문 조각 전투 화면 Figma 생성 플러그인.
// 메뉴 "레이아웃 수정안": Frame 34(213:420) 안에 1920x1080 수정안 3종을 만든다.
// 메뉴 "판타지 UI 키트": Frame 34가 있는 페이지에 C안 기준 UI 키트 3종을 만든다.
// 다시 실행하면 이 플러그인이 이전에 만든 결과만 지우고 새로 만든다.

const TARGET_ID = '213:420';
const ROOT_NAME = '전투 UI 레이아웃 수정안 (자동 생성)';
const KIT_ROOT_NAME = '판타지 UI 키트 3안 · C안 기준 (자동 생성)';
const BASE_FONT = 'Noto Sans KR';
const SW = 1920, SH = 1080, GAP = 160;

// 테마가 바꾸는 값. 기본값은 레이아웃 수정안에서 쓰는 색이다.
const BASE = {
  bg: '#161D26', panel: '#212B37', panel2: '#283442', line: '#34424F',
  text: '#EBEFF5', sub: '#A9B4C2', dim: '#738091', ink: '#141A21',
  cellBg: '#1D2630', cellLine: '#2B3642', empty: '#5B6F86',
  normal: '#B9C4CE', flame: '#E07A3F', guard: '#4F95D6', resonance: '#A777D9', afterimage: '#55C7B3',
  hp: '#E0605A', hpTrack: '#2E2226', hpPlayer: '#6CCB7A', hpPlayerTrack: '#1F2E25',
  intent: '#F07167', intentBg: '#3A2328', def: '#8FB3D9', ok: '#7BE0A0', warn: '#F2B84B', danger: '#E0605A',
  primary: '#4D6B8F', primaryText: '#EBEFF5', accent: '#A9B4C2', enemy: ['#7A6A8C', '#5F7F72', '#8C6E5E'],
  desk: '#1A222C', field: '#1C2530', portrait: '#2A3644', pileBack1: '#222C38', pileBack2: '#26313E',
  pileFront: '#2C3846', pileLine: '#46566A', warnBg: '#3A301E', dangerBg: '#3A2328', barText: '#FFFFFF',
};
const BASE_PAT = [
  { name: '불씨', color: '#F4C95D' },
  { name: '방벽', color: '#E6EBF0' },
  { name: '번개', color: '#B9E36B' },
  { name: '유성', color: '#F28DB2' },
];
let C = { ...BASE };
let PAT = BASE_PAT;
let FONT = BASE_FONT;
let HEAD = BASE_FONT;
let DECOR = null;
let KIT = null;
function deco(node, role, extra) { if (DECOR && DECOR[role]) DECOR[role](node, extra); return node; }

// 세 안이 공유하는 보드 상태. 마스크 비트는 위 1, 오른쪽 2, 아래 4, 왼쪽 8이다.
const FULL = [0, 1, 2, 3];
const BOARD = {
  patterns: [
    { i: 0, cells: { '0,0': 4, '0,1': 15 }, complete: true, cast: [0.5, 1.15] },
    { i: 1, cells: { '1,0': 15, '1,1': 1 }, complete: false },
    { i: 2, cells: { '2,0': 15, '2,1': 15, '3,1': 15 }, complete: true, cast: [3.0, 1.5] },
    { i: 3, cells: { '4,1': 15, '5,1': 15, '4,2': 15, '5,2': 15 }, complete: false },
  ],
  pieces: [
    { sectors: FULL.map(s => [0, 1, s]), color: 'normal' },
    { sectors: [[0, 0, 2]], color: 'normal' },
    { sectors: FULL.map(s => [1, 0, s]), color: 'guard', icon: 'guard' },
    { sectors: [[2, 0, 0], [2, 0, 1]], color: 'flame', icon: 'flame' },
    { sectors: [[2, 0, 2]], color: 'afterimage', icon: 'afterimage' },
    { sectors: [[2, 0, 3]], color: 'afterimage', icon: 'afterimage' },
    { sectors: FULL.map(s => [2, 1, s]).concat(FULL.map(s => [3, 1, s])), color: 'resonance', icon: 'resonance' },
  ],
};
const INFO = [
  { short: '피해 6', status: '완성', lines: [['기본', '선택한 적에게 피해 6']], total: '피해 6' },
  { short: '방어 10', status: '1/4칸 남음', lines: [['기본', '방어 7'], ['수호 ×1', '방어 +3']], total: '방어 10' },
  { short: '피해 14 · 배치 +1', status: '완성', lines: [['기본', '선택한 적에게 피해 12'], ['불꽃 ×1', '공격 피해 +2'], ['공명 ×1', '3칸 이상 주문 · 배치 +1'], ['잔상 ×2', '첫 시전 후 판에 1회 남음']], total: '피해 14 · 배치 +1 · 잔상 2장 유지' },
  { short: '전체 피해 10', status: '4칸 남음', lines: [['기본', '살아 있는 모든 적에게 피해 10']], total: '전체 피해 10' },
];
const HAND = [
  { shape: 'quarter', name: '1/4 삼각형' },
  { shape: 'quarter', name: '1/4 삼각형' },
  { shape: 'half', name: '반칸 삼각형' },
  { shape: 'square', name: '정사각형' },
  { shape: 'rect', name: '직사각형' },
  { shape: 'half', name: '불꽃 삼각형', fx: 'flame', fxText: '공격 피해 +2' },
  { shape: 'quarter', name: '잔상 1/4 삼각형', fx: 'afterimage', fxText: '시전 후 1회 유지' },
];

// ---------- 기본 도형 ----------
const r2 = v => Math.round(v * 100) / 100;
function hex(h) { return { r: parseInt(h.slice(1, 3), 16) / 255, g: parseInt(h.slice(3, 5), 16) / 255, b: parseInt(h.slice(5, 7), 16) / 255 }; }
function paint(h, o = 1) { return [{ type: 'SOLID', color: hex(h), opacity: o }]; }
function add(parent, node, name) { parent.appendChild(node); node.name = name; return node; }
function styleBox(n, fill, o) {
  n.fills = fill ? paint(fill, o.opacity ?? 1) : [];
  if (o.r) n.cornerRadius = o.r;
  if (o.stroke) { n.strokes = paint(o.stroke, o.strokeOpacity ?? 1); n.strokeWeight = o.sw || 1; n.strokeAlign = 'INSIDE'; if (o.dash) n.dashPattern = o.dash; }
  if (o.alpha !== undefined) n.opacity = o.alpha;
  if (o.shadow) n.effects = [{ type: 'DROP_SHADOW', color: { r: 0, g: 0, b: 0, a: 0.45 }, offset: { x: 0, y: 10 }, radius: 28, spread: 0, visible: true, blendMode: 'NORMAL' }];
}
function frame(p, name, x, y, w, h, fill, o = {}) { const n = add(p, figma.createFrame(), name); n.resize(w, h); n.x = x; n.y = y; n.clipsContent = !!o.clip; styleBox(n, fill, o); return n; }
function rect(p, name, x, y, w, h, fill, o = {}) { const n = add(p, figma.createRectangle(), name); n.resize(w, h); n.x = x; n.y = y; styleBox(n, fill, o); return n; }
function ellipse(p, name, x, y, w, h, fill, o = {}) { const n = add(p, figma.createEllipse(), name); n.resize(w, h); n.x = x; n.y = y; styleBox(n, fill, o); return n; }
function vec(p, name, segments, fill, o = {}) {
  // segments: 점 배열들의 배열. 각 배열이 하나의 하위 경로가 된다.
  const all = segments.flat();
  const minX = Math.min(...all.map(q => q[0])), minY = Math.min(...all.map(q => q[1]));
  const data = segments.map(seg => seg.map((q, i) => `${i ? 'L' : 'M'} ${r2(q[0] - minX)} ${r2(q[1] - minY)}`).join(' ') + (o.open ? '' : ' Z')).join(' ');
  const n = add(p, figma.createVector(), name);
  n.vectorPaths = [{ windingRule: 'NONZERO', data }];
  n.x = minX; n.y = minY;
  n.fills = fill ? paint(fill, o.opacity ?? 1) : [];
  if (o.stroke) { n.strokes = paint(o.stroke, o.strokeOpacity ?? 1); n.strokeWeight = o.sw || 1; n.strokeCap = o.cap || 'ROUND'; n.strokeJoin = 'ROUND'; if (o.dash) n.dashPattern = o.dash; }
  if (o.alpha !== undefined) n.opacity = o.alpha;
  return n;
}
function poly(p, name, pts, fill, o = {}) { return vec(p, name, [pts], fill, o); }
function lines(p, name, segs, color, sw, o = {}) { return vec(p, name, segs, null, { ...o, open: true, stroke: color, sw }); }
function pathVec(p, name, x, y, data, o = {}) {
  const n = add(p, figma.createVector(), name);
  n.vectorPaths = [{ windingRule: 'NONZERO', data }];
  n.x = x; n.y = y; n.fills = [];
  n.strokes = paint(o.stroke, 1); n.strokeWeight = o.sw || 2; n.strokeCap = 'ROUND';
  if (o.dash) n.dashPattern = o.dash;
  return n;
}
function txt(p, name, x, y, w, value, size, o = {}) {
  const n = add(p, figma.createText(), name);
  n.fontName = { family: o.head ? HEAD : FONT, style: o.bold ? 'Bold' : 'Regular' };
  n.fontSize = size;
  n.lineHeight = { unit: 'PIXELS', value: Math.round(size * (o.lh || 1.4)) };
  n.characters = value;
  n.fills = paint(o.color || C.text, o.opacity ?? 1);
  n.textAlignHorizontal = o.align || 'LEFT';
  if (o.h) { n.resize(w, o.h); n.textAutoResize = 'NONE'; n.textAlignVertical = o.valign || 'CENTER'; }
  else if (w) { n.resize(w, 10); n.textAutoResize = 'HEIGHT'; }
  else n.textAutoResize = 'WIDTH_AND_HEIGHT';
  n.x = x; n.y = y;
  return n;
}
function button(p, name, x, y, w, h, label, o = {}) {
  const b = frame(p, name, x, y, w, h, o.fill || C.panel2, { r: o.r ?? 10, stroke: o.stroke, sw: o.sw, alpha: o.alpha });
  txt(b, '라벨', 0, 0, w, label, o.size || 16, { h, align: 'CENTER', bold: o.bold, color: o.color || C.text });
  return b;
}

// ---------- 아이콘 ----------
function sword(p, x, y, s, color) {
  const g = frame(p, '아이콘 · 공격', x, y, s, s, null);
  poly(g, '칼날', [[s * 0.5, 0], [s * 0.64, s * 0.6], [s * 0.5, s * 0.68], [s * 0.36, s * 0.6]], color);
  rect(g, '코등이', s * 0.18, s * 0.64, s * 0.64, s * 0.1, color, { r: 2 });
  rect(g, '손잡이', s * 0.43, s * 0.74, s * 0.14, s * 0.26, color, { r: 2 });
  return g;
}
function shield(p, x, y, s, color) {
  const g = frame(p, '아이콘 · 방어', x, y, s, s, null);
  poly(g, '방패', [[s * 0.08, s * 0.12], [s * 0.5, 0], [s * 0.92, s * 0.12], [s * 0.86, s * 0.58], [s * 0.5, s], [s * 0.14, s * 0.58]], color);
  return g;
}
function fxIcon(p, kind, x, y, s, color) {
  const g = frame(p, '특수 표식 · ' + kind, x, y, s, s, null);
  if (kind === 'flame') poly(g, '불꽃', [[s * 0.5, 0], [s * 0.86, s * 0.5], [s * 0.78, s * 0.86], [s * 0.5, s], [s * 0.22, s * 0.86], [s * 0.14, s * 0.5]], color);
  else if (kind === 'guard') { poly(g, '방패', [[s * 0.1, s * 0.12], [s * 0.5, 0], [s * 0.9, s * 0.12], [s * 0.84, s * 0.58], [s * 0.5, s], [s * 0.16, s * 0.58]], color); }
  else if (kind === 'resonance') { ellipse(g, '고리', 0, 0, s, s, null, { stroke: color, sw: Math.max(2, s * 0.14) }); ellipse(g, '핵', s * 0.34, s * 0.34, s * 0.32, s * 0.32, color); }
  else if (kind === 'afterimage') { poly(g, '잔상 뒤', [[s * 0.42, 0], [s * 0.84, s * 0.42], [s * 0.42, s * 0.84], [0, s * 0.42]], null, { stroke: color, sw: 2 }); poly(g, '잔상 앞', [[s * 0.58, s * 0.16], [s, s * 0.58], [s * 0.58, s], [s * 0.16, s * 0.58]], color); }
  return g;
}
function grip(p, x, y, color) {
  const g = frame(p, '이동 핸들', x, y, 10, 16, null);
  for (let r = 0; r < 3; r++) for (let c = 0; c < 2; c++) ellipse(g, '점', c * 6, r * 6, 4, 4, color);
  return g;
}
function diamond(p, x, y, s, filled, color) {
  const pts = [[x + s / 2, y], [x + s, y + s / 2], [x + s / 2, y + s], [x, y + s / 2]];
  return filled ? poly(p, '배치 · 남음', pts, color) : poly(p, '배치 · 사용', pts, null, { stroke: C.dim, sw: 2 });
}

// ---------- 격자 ----------
function corners(x, y, S) { const px = x * S, py = y * S; return [[px, py], [px + S, py], [px + S, py + S], [px, py + S]]; }
function sectorPts(x, y, s, S) { const c = corners(x, y, S); return [c[s], c[(s + 1) % 4], [x * S + S / 2, y * S + S / 2]]; }
function edges(set, S) {
  const segs = [], dir = [[0, -1], [1, 0], [0, 1], [-1, 0]];
  for (const k of set) {
    const [x, y, s] = k.split(',').map(Number), cs = corners(x, y, S), ctr = [x * S + S / 2, y * S + S / 2];
    if (!set.has(`${x + dir[s][0]},${y + dir[s][1]},${(s + 2) % 4}`)) segs.push([cs[s], cs[(s + 1) % 4]]);
    if (!set.has(`${x},${y},${(s + 1) % 4}`)) segs.push([ctr, cs[(s + 1) % 4]]);
    if (!set.has(`${x},${y},${(s + 3) % 4}`)) segs.push([ctr, cs[s]]);
  }
  return segs;
}
function patternSet(pt) { const set = new Set(); for (const [k, m] of Object.entries(pt.cells)) for (let s = 0; s < 4; s++) if (m & (1 << s)) set.add(`${k},${s}`); return set; }
function patternBox(pt) { const xs = Object.keys(pt.cells).map(k => +k.split(',')[0]), ys = Object.keys(pt.cells).map(k => +k.split(',')[1]); return { x0: Math.min(...xs), y0: Math.min(...ys), x1: Math.max(...xs) + 1, y1: Math.max(...ys) + 1 }; }

function drawGrid(p, gx, gy, S, o = {}) {
  const g = frame(p, '마법진 격자 3×6', gx, gy, 6 * S, 3 * S, null);
  for (let y = 0; y < 3; y++) for (let x = 0; x < 6; x++) {
    rect(g, `칸 ${x},${y}`, x * S, y * S, S, S, C.cellBg, { stroke: C.cellLine, sw: 1 });
  }
  const filled = new Set();
  for (const pc of BOARD.pieces) for (const [x, y, s] of pc.sectors) filled.add(`${x},${y},${s}`);
  for (const pt of BOARD.patterns) {
    const set = patternSet(pt), empties = [...set].filter(k => !filled.has(k));
    if (empties.length) vec(g, `${PAT[pt.i].name} · 빈 필수 구역`, empties.map(k => { const [x, y, s] = k.split(',').map(Number); return sectorPts(x, y, s, S); }), C.empty);
  }
  // 빈 필수 구역 위에도 칸과 1/4 구역 경계가 보이도록 구분선을 채우기 뒤에 그린다.
  const guide = [];
  for (let y = 0; y < 3; y++) for (let x = 0; x < 6; x++) { const c = corners(x, y, S); guide.push([c[0], c[2]], [c[1], c[3]], [c[0], c[1]], [c[0], c[3]]); }
  guide.push([[6 * S, 0], [6 * S, 3 * S]], [[0, 3 * S], [6 * S, 3 * S]]);
  lines(g, '칸·구역 구분선', guide, C.cellLine, 1);
  for (const pc of BOARD.pieces) {
    const set = new Set(pc.sectors.map(a => a.join(','))), color = C[pc.color];
    vec(g, '조각', pc.sectors.map(([x, y, s]) => sectorPts(x, y, s, S)), color, { stroke: color, sw: 1 });
    lines(g, '조각 경계', edges(set, S), C.ink, 2);
    if (pc.icon) {
      const pts = pc.sectors.flatMap(([x, y, s]) => sectorPts(x, y, s, S));
      const cx = pts.reduce((a, q) => a + q[0], 0) / pts.length, cy = pts.reduce((a, q) => a + q[1], 0) / pts.length;
      const is = pc.sectors.length === 1 ? 14 : 20;
      fxIcon(g, pc.icon, cx - is / 2, cy - is / 2, is, C.ink);
    }
  }
  for (const pt of BOARD.patterns) lines(g, `${PAT[pt.i].name} · 도안 외곽선`, edges(patternSet(pt), S), PAT[pt.i].color, pt.complete ? 5 : 4);
  if (o.preview) vec(g, '배치 미리보기 · 가능', o.preview.map(([x, y, s]) => sectorPts(x, y, s, S)), C.ok, { opacity: 0.85 });
  return g;
}
// 키트 컴포넌트가 있으면 인스턴스를 놓고 지정한 이름의 글자만 바꾼다. 없으면 null을 돌려준다.
function kitInst(group, key, parent, x, y, texts) {
  if (!KIT || !KIT[group] || !KIT[group][key]) return null;
  const n = KIT[group][key].createInstance();
  parent.appendChild(n); n.x = x; n.y = y;
  if (texts) for (const [name, value] of Object.entries(texts)) {
    const t = n.findOne(c => c.type === 'TEXT' && c.name === name);
    if (t) t.characters = value;
  }
  return n;
}
function castFace(p, name, x, y, w, label, color, active) {
  const h = 44, b = frame(p, name, x, y, w, h, color, { r: 22, stroke: active ? '#FFFFFF' : C.ink, sw: active ? 3 : 2, shadow: true });
  poly(b, '시전 표식', [[18, 14], [32, 22], [18, 30]], C.ink);
  txt(b, '라벨', 30, 0, w - 38, label, 17, { h, align: 'CENTER', bold: true, color: C.ink });
  return b;
}
function castButton(p, gx, gy, S, pt, label, o = {}) {
  const w = o.w || 104, h = 44, cx = gx + pt.cast[0] * S, cy = gy + pt.cast[1] * S;
  const inst = kitInst('cast', `${PAT[pt.i].name}|${o.active ? '조준 중' : '기본'}`, p, 0, 0);
  if (inst) { inst.x = cx - inst.width / 2; inst.y = cy - inst.height / 2; return inst; }
  return castFace(p, `${PAT[pt.i].name} · 시전 버튼`, cx - w / 2, cy - h / 2, w, label, PAT[pt.i].color, o.active);
}
function intentBadge(p, x, y, value) {
  const badge = frame(p, '공격 의도', x, y, 112, 48, C.intentBg, { r: 24, stroke: C.intent, sw: 2 });
  sword(badge, 16, 10, 28, C.intent);
  txt(badge, '공격 수치', 48, 0, 52, String(value), 28, { h: 48, bold: true, color: C.intent });
  return badge;
}

// ---------- 공통 화면 요소 ----------
function topBar(s, battle) {
  txt(s, '전투 진행', 40, 26, 0, `전투 ${battle}/6`, 22, { bold: true, head: true });
  for (let i = 0; i < 6; i++) {
    const x = 160 + i * 20, y = 38;
    if (i < battle - 1) ellipse(s, '진행 점 · 완료', x, y, 10, 10, C.sub);
    else if (i === battle - 1) ellipse(s, '진행 점 · 현재', x - 2, y - 2, 14, 14, null, { stroke: C.text, sw: 2 });
    else ellipse(s, '진행 점 · 남음', x, y, 10, 10, C.line);
  }
  const help = frame(s, '도움말 버튼', 1688, 20, 48, 48, C.panel2, { r: 24 });
  txt(help, '라벨', 0, 0, 48, '?', 22, { h: 48, align: 'CENTER', bold: true });
  button(s, '메뉴 버튼', 1748, 20, 132, 48, '메뉴  Esc', { size: 16, r: 24 });
}
function silhouette(p, x, y, w, h, color, boss) {
  const g = frame(p, '전신 실루엣', x, y, w, h, null);
  rect(g, '머리', w * 0.32, 0, w * 0.36, h * 0.24, color, { r: 6 });
  rect(g, '몸통', w * 0.19, h * 0.27, w * 0.62, h * 0.43, color, { r: 8 });
  rect(g, '왼팔', 0, h * 0.31, w * 0.16, h * 0.34, color, { r: 6 });
  rect(g, '오른팔', w * 0.84, h * 0.31, w * 0.16, h * 0.34, color, { r: 6 });
  rect(g, '왼다리', w * 0.24, h * 0.72, w * 0.2, h * 0.28, color, { r: 6 });
  rect(g, '오른다리', w * 0.56, h * 0.72, w * 0.2, h * 0.28, color, { r: 6 });
  rect(g, '눈', w * 0.4, h * 0.09, w * 0.2, h * 0.04, '#F2E8CB', { r: 2 });
  if (boss) { poly(g, '왕관', [[w * 0.3, h * 0.02], [w * 0.36, -h * 0.07], [w * 0.44, h * 0.0], [w * 0.5, -h * 0.09], [w * 0.56, h * 0.0], [w * 0.64, -h * 0.07], [w * 0.7, h * 0.02]], '#D9B45A'); }
  return g;
}
function enemyUnit(s, cx, groundY, e) {
  // 보스는 왕관이 공격 의도 배지와 겹치지 않도록 실루엣을 더 아래에 둔다.
  const pad = e.boss ? 100 : 64;
  const UW = Math.max(e.w + 60, 260), top = groundY - e.h - pad;
  const u = frame(s, `적 · ${e.name}`, cx - UW / 2, top, UW, e.h + pad + 90, null);
  if (!e.dead) {
    const bw = 112;
    if (!kitInst('intent', '기본', u, UW / 2 - bw / 2, 0, { '공격 수치': String(e.intent) })) intentBadge(u, UW / 2 - bw / 2, 0, e.intent);
    if (e.pattern) txt(u, '공격 패턴', UW / 2 + bw / 2 + 10, 14, 0, e.pattern, 13, { color: C.sub });
  }
  const sil = silhouette(u, UW / 2 - e.w / 2, pad, e.w, e.h, e.color, e.boss);
  if (e.dead) sil.opacity = 0.22;
  if (e.aim) {
    rect(u, '조준 강조', UW / 2 - e.w / 2 - 22, pad - 50, e.w + 44, e.h + 64, PAT[2].color, { r: 16, opacity: 0.08, stroke: PAT[2].color, sw: 3, dash: [12, 8] });
  }
  const ny = pad + e.h + (e.aim ? 30 : 14);
  txt(u, '이름', 0, ny, UW, e.dead ? `${e.name} · 쓰러짐` : e.name, e.boss ? 22 : 18, { align: 'CENTER', bold: true, head: true, color: e.dead ? C.dim : C.text });
  if (!e.dead) {
    const bw = UW - 40, by = ny + (e.boss ? 38 : 32);
    rect(u, '체력 바 · 바탕', 20, by, bw, 24, C.hpTrack, { r: 6 });
    rect(u, '체력 바 · 현재', 20, by, Math.max(6, bw * e.hp / e.max), 24, C.hp, { r: 6 });
    if (e.preview) {
      const after = Math.max(0, e.hp - e.preview), x0 = 20 + bw * after / e.max;
      rect(u, '체력 바 · 예상 피해', x0, by, bw * (e.hp - after) / e.max, 24, '#FFFFFF', { opacity: 0.55 });
    }
    txt(u, '체력 수치', 20, by, bw, e.preview ? `${e.hp} → ${Math.max(0, e.hp - e.preview)} / ${e.max}` : `${e.hp} / ${e.max}`, 15, { h: 24, align: 'CENTER', bold: true, color: C.barText });
    if (e.target) {
      const chip = frame(u, '현재 대상', UW / 2 + e.w / 2 - 18, pad, 52, 26, C.text, { r: 13 });
      txt(chip, '라벨', 0, 0, 52, '대상', 13, { h: 26, align: 'CENTER', bold: true, color: C.ink });
    }
  }
  return u;
}
function wizardPortrait(p, x, y, size) {
  const f = frame(p, '플레이어 초상화', x, y, size, size, C.portrait, { r: 14, clip: true, stroke: C.line, sw: 2 });
  poly(f, '망토', [[size * 0.14, size], [size * 0.3, size * 0.62], [size * 0.7, size * 0.62], [size * 0.86, size]], '#5C7886');
  poly(f, '후드', [[size * 0.22, size * 0.66], [size * 0.5, size * 0.1], [size * 0.78, size * 0.66]], '#748D98');
  ellipse(f, '얼굴', size * 0.37, size * 0.38, size * 0.26, size * 0.24, '#D5C4A6');
  return f;
}
function placements(s, x, y, left, o = {}) {
  const inst = kitInst('placement', String(Math.min(left, 4)), s, x, y);
  if (inst) { if (o.note) txt(s, '배치 안내', x, y + 38, 360, o.note, 14, { color: C.warn }); return inst; }
  return placementFace(s, x, y, left, o);
}
function placementFace(s, x, y, left, o = {}) {
  const g = frame(s, '남은 배치', x, y, 300, 30, null);
  txt(g, '라벨', 0, 0, 0, '배치', 16, { color: C.sub, bold: true });
  for (let i = 0; i < 3; i++) diamond(g, 48 + i * 30, 4, 22, i < left, C.text);
  let tx = 48 + 3 * 30 + 4;
  if (left > 3) { txt(g, '추가 배치', tx, 0, 0, `+${left - 3}`, 18, { bold: true, color: C.ok }); tx += 34; }
  txt(g, '배치 수치', tx, 0, 0, left > 3 ? `${left}회` : `${left}/3`, 18, { bold: true, color: left === 0 ? C.warn : C.text });
  if (o.note) txt(g, '배치 안내', 0, 32, 360, o.note, 14, { color: C.warn });
  return g;
}
function playerStatus(s, x, y, o = {}) {
  const g = frame(s, '플레이어 상태', x, y, 400, 128, null);
  wizardPortrait(g, 0, 0, o.size || 128);
  const bx = (o.size || 128) + 20;
  txt(g, '이름', bx, 4, 0, '마법사', 18, { bold: true, head: true });
  rect(g, '체력 바 · 바탕', bx, 38, 210, 26, C.hpPlayerTrack, { r: 6 });
  rect(g, '체력 바 · 현재', bx, 38, 210 * (o.hp ?? 38) / 50, 26, C.hpPlayer, { r: 6 });
  txt(g, '체력 수치', bx, 38, 210, `체력 ${o.hp ?? 38} / 50`, 15, { h: 26, align: 'CENTER', bold: true, color: C.barText });
  shield(g, bx, 78, 26, C.def);
  txt(g, '방어 수치', bx + 34, 76, 0, `방어 ${o.def ?? 0}`, 18, { bold: true, color: C.def });
  return g;
}
function pieceIcon(p, card, x, y) {
  const color = card.fx ? C[card.fx] : C.normal;
  const g = frame(p, '조각 모양', x, y, 88, 64, null);
  if (card.shape === 'rect') {
    const k = 40, ox = 4, oy = 12;
    rect(g, '칸 기준선', ox, oy, k * 2, k, null, { stroke: C.dim, sw: 1, dash: [3, 3] });
    rect(g, '조각', ox, oy, k * 2, k, color);
  } else {
    const k = 56, ox = 16, oy = 4;
    rect(g, '칸 기준선', ox, oy, k, k, null, { stroke: C.dim, sw: 1, dash: [3, 3] });
    if (card.shape === 'square') rect(g, '조각', ox, oy, k, k, color);
    if (card.shape === 'quarter') poly(g, '조각', [[ox, oy], [ox + k, oy], [ox + k / 2, oy + k / 2]], color);
    if (card.shape === 'half') poly(g, '조각', [[ox, oy], [ox + k, oy], [ox + k, oy + k]], color);
  }
  if (card.fx) fxIcon(g, card.fx, 70, 46, 16, color);
  return g;
}
function handCard(s, x, y, card, o = {}) {
  const inst = kitInst('card', `${card.name}|${o.dim ? '배치 불가' : '기본'}`, s, x, y);
  if (inst) return inst;
  const c = frame(s, `손패 · ${card.name}`, x, y, 104, 150, C.panel2, { r: 10, stroke: card.fx ? C[card.fx] : C.line, sw: card.fx ? 2 : 1, alpha: o.dim ? 0.42 : 1 });
  deco(c, 'card');
  pieceIcon(c, card, 8, 10);
  txt(c, '이름', 4, 80, 96, card.name, 14, { align: 'CENTER', bold: !!card.fx, lh: 1.3 });
  if (card.fx) {
    const chip = frame(c, '특수 효과', 6, 120, 92, 22, C[card.fx], { r: 6, opacity: 0.22 });
    txt(chip, '라벨', 0, 0, 92, card.fxText, 11, { h: 22, align: 'CENTER', color: C[card.fx], bold: true });
  }
  return c;
}
function emptySlot(s, x, y) { return rect(s, '손패 · 드래그 중인 자리', x, y, 104, 150, null, { r: 10, stroke: C.dim, sw: 2, dash: [8, 6] }); }
function pile(s, x, y, label, count) {
  const inst = kitInst('pile', label, s, x, y, { '수량': String(count) });
  if (inst) return inst;
  const g = frame(s, `더미 · ${label}`, x, y, 104, 150, null);
  rect(g, '뒷장 2', 8, 0, 92, 136, C.pileBack1, { r: 10, stroke: C.line });
  rect(g, '뒷장 1', 4, 5, 92, 136, C.pileBack2, { r: 10, stroke: C.line });
  const f = frame(g, '앞장', 0, 10, 92, 136, C.pileFront, { r: 10, stroke: C.pileLine, sw: 2 });
  deco(f, 'card');
  txt(f, '수량', 0, 26, 92, String(count), 34, { align: 'CENTER', bold: true, head: true });
  txt(f, '이름', 0, 84, 92, label, 14, { align: 'CENTER', color: C.sub });
  txt(f, '열람 안내', 0, 106, 92, '클릭해 보기', 11, { align: 'CENTER', color: C.dim });
  return g;
}
function handRow(s, x, y, o = {}) {
  pile(s, x, y, '드로우 덱', o.draw ?? 9);
  const hx = x + 124;
  HAND.forEach((card, i) => {
    const cx = hx + i * 114;
    if (o.lifted === i) emptySlot(s, cx, y); else handCard(s, cx, y, card, { dim: o.dim });
  });
  pile(s, hx + 7 * 114 + 10, y, '묘지', o.discard ?? 3);
  return hx;
}
function endTurn(s, x, y, turn, incoming, def) {
  const taken = Math.max(0, incoming - def);
  const g = frame(s, '턴 종료 영역', x, y, 300, 140, null);
  sword(g, 0, 6, 22, C.intent);
  txt(g, '공격 예고', 32, 2, 268, `적 공격 ${incoming}  ·  받을 피해 ${taken}`, 16, { color: C.sub });
  if (!kitInst('endTurn', '기본', g, 0, 44, { '라벨': `${turn}턴 종료` })) endTurnFace(g, 0, 44, `${turn}턴 종료`);
  return g;
}
function endTurnFace(p, x, y, label) {
  const b = button(p, '턴 종료 버튼', x, y, 300, 88, label, { size: 28, bold: true, fill: C.primary, r: 14, color: C.primaryText });
  b.children[0].fontName = { family: HEAD, style: 'Bold' };
  return deco(b, 'primary');
}
function toast(s, cx, y, text) {
  const w = 470, g = frame(s, '토스트 · 회수 불가', cx - w / 2, y, w, 40, C.warnBg, { r: 20, stroke: C.warn, sw: 2 });
  ellipse(g, '경고 원', 14, 9, 22, 22, C.warn);
  txt(g, '경고 표식', 14, 9, 22, '!', 15, { h: 22, align: 'CENTER', bold: true, color: C.ink });
  txt(g, '내용', 46, 0, w - 60, text, 15, { h: 40, color: C.text });
  return g;
}
function boardCount(s, x, y, w) {
  const inst = kitInst('chip', '기본', s, x, y);
  if (inst) return inst;
  const g = frame(s, '판 위 조각', x, y, w, 32, C.panel, { r: 16, stroke: C.line });
  txt(g, '라벨', 0, 0, w, '판 위 조각 7장 · 다음 턴에도 유지', 13, { h: 32, align: 'CENTER', color: C.sub });
  return g;
}
function tooltipShadowLine(s, a, b, color) { return lines(s, '연결선', [[a, b]], color, 2, { dash: [4, 4] }); }

// ---------- 안 A: 정돈된 3단 ----------
function buildA(s) {
  topBar(s, 4);
  enemyUnit(s, 780, 360, { name: '거대 잉크', hp: 26, max: 26, intent: 8, pattern: '패턴 2 · 8', w: 190, h: 230, color: C.enemy[1], target: true });
  enemyUnit(s, 1140, 360, { name: '잉크 하수인', hp: 8, max: 12, intent: 4, w: 150, h: 190, color: C.enemy[0] });
  const S = 112, gx = 624, gy = 512;
  drawGrid(s, gx, gy, S);
  for (const pt of BOARD.patterns) {
    const b = patternBox(pt), w = (b.x1 - b.x0) * S - 6, x = gx + b.x0 * S + 3, y = gy + b.y0 * S - 54;
    const tab = frame(s, `${PAT[pt.i].name} · 도안 탭`, x, y, w, 50, C.panel, { r: 8, stroke: PAT[pt.i].color, sw: 2 });
    grip(tab, 8, 8, C.dim);
    txt(tab, '이름', 24, 2, w - 56, PAT[pt.i].name, 16, { bold: true, color: PAT[pt.i].color });
    txt(tab, '효과 요약', 24, 25, w - 30, INFO[pt.i].short, 13, { color: C.sub, h: 20, valign: 'TOP' });
    button(tab, '더보기 메뉴', w - 32, 4, 28, 22, '···', { size: 14, fill: C.panel2, r: 6, color: C.sub });
  }
  castButton(s, gx, gy, S, BOARD.patterns[0], '시전');
  castButton(s, gx, gy, S, BOARD.patterns[2], '시전');
  // 번개 도안에 마우스를 올린 상태의 상세 팝업. 격자 밖 오른쪽에 두어 배치 구역을 가리지 않는다.
  const px = gx + 6 * S + 28, py = gy;
  tooltipShadowLine(s, [gx + 3 * S, gy + S * 0.5], [px, py + 40], PAT[2].color);
  const pop = frame(s, '번개 · 상세 팝업 (마우스 올림)', px, py, 340, 10, C.panel, { r: 12, stroke: PAT[2].color, sw: 2, shadow: true });
  txt(pop, '제목', 18, 14, 0, '번개 · 완성', 18, { bold: true, color: PAT[2].color });
  let ly = 50;
  for (const [k, v] of INFO[2].lines) {
    txt(pop, '항목', 18, ly, 76, k, 14, { color: C.sub });
    const t = txt(pop, '값', 98, ly, 224, v, 14);
    ly += t.height + 8;
  }
  rect(pop, '구분선', 18, ly + 2, 304, 1, C.line);
  const tot = txt(pop, '합계', 18, ly + 12, 304, `합계  ${INFO[2].total}`, 15, { bold: true });
  pop.resize(340, ly + 12 + tot.height + 16);
  boardCount(s, gx - 278, gy + 3 * S - 32, 262);
  toast(s, 960, 850, '손패가 7장이라 조각을 회수할 수 없습니다');
  placements(s, 40, 858, 2);
  playerStatus(s, 40, 902, { hp: 38, def: 0 });
  handRow(s, 420, 900);
  endTurn(s, 1580, 896, 2, 12, 0);
}

// ---------- 안 B: 도안 원장 ----------
function buildB(s) {
  topBar(s, 2);
  enemyUnit(s, 420, 360, { name: '잉크 하수인', hp: 12, max: 12, intent: 4, w: 150, h: 190, color: C.enemy[0], target: true });
  enemyUnit(s, 720, 360, { name: '거대 잉크', hp: 21, max: 26, intent: 2, pattern: '패턴 2 · 8', w: 190, h: 230, color: C.enemy[1] });
  enemyUnit(s, 1020, 360, { name: '잉크 하수인', hp: 0, max: 12, intent: 0, w: 150, h: 190, color: C.enemy[0], dead: true });
  txt(s, '검증용 3마리 배치', 1060, 84, 0, '적 3마리 · 쓰러진 슬롯 유지 확인용', 13, { color: C.dim });
  const S = 112, gx = 384, gy = 512;
  drawGrid(s, gx, gy, S, { preview: [[1, 1, 0]] });
  for (const pt of BOARD.patterns) {
    const b = patternBox(pt), w = (b.x1 - b.x0) * S - 6, x = gx + b.x0 * S + 3, y = gy + b.y0 * S - 36;
    const tab = frame(s, `${PAT[pt.i].name} · 도안 탭`, x, y, w, 32, PAT[pt.i].color, { r: 8, opacity: 0.2, stroke: PAT[pt.i].color, sw: 2 });
    grip(tab, 8, 8, PAT[pt.i].color);
    txt(tab, '이름', 24, 0, w - 30, PAT[pt.i].name, 15, { h: 32, bold: true, color: PAT[pt.i].color });
  }
  castButton(s, gx, gy, S, BOARD.patterns[0], '시전');
  castButton(s, gx, gy, S, BOARD.patterns[2], '시전');
  // 드래그 중: 손패의 1/4 삼각형을 방벽 빈 구역 위로 가져온 상태.
  const hint = frame(s, '드래그 중 조작 안내', gx + 8, gy + 2 * S + 20, 280, 36, C.ink, { r: 8, stroke: C.line, alpha: 0.95 });
  txt(hint, '라벨', 12, 0, 260, 'R · 우클릭 회전   손패에 놓으면 취소', 14, { h: 36, color: C.text });
  poly(s, '커서', [[gx + 1.5 * S + 6, gy + S + 10], [gx + 1.5 * S + 6, gy + S + 34], [gx + 1.5 * S + 12, gy + S + 28], [gx + 1.5 * S + 18, gy + S + 38], [gx + 1.5 * S + 22, gy + S + 36], [gx + 1.5 * S + 16, gy + S + 26], [gx + 1.5 * S + 24, gy + S + 26]], '#FFFFFF', { stroke: C.ink, sw: 1 });

  // 오른쪽 도안 원장. 효과 설명이 길어져도 줄바꿈으로 세로로만 늘어난다.
  const PX = 1440, PW = 448;
  const panel = frame(s, '도안 원장 패널', PX, 92, PW, 760, C.panel, { r: 16, stroke: C.line });
  txt(panel, '제목', 24, 18, 0, '보유 도안', 20, { bold: true });
  txt(panel, '안내', 24, 50, 400, '행에 마우스를 올리면 격자의 도안을 강조합니다', 13, { color: C.dim });
  let ry = 86;
  let menuAnchor = null;
  for (const pt of BOARD.patterns) {
    const row = frame(panel, `${PAT[pt.i].name} · 원장 행`, 16, ry, PW - 32, 10, pt.i === 2 ? C.panel2 : null, { r: 10, stroke: pt.i === 2 ? PAT[2].color : C.line, sw: pt.i === 2 ? 2 : 1 });
    rect(row, '색 표식', 0, 0, 6, 10, PAT[pt.i].color, { r: 3 });
    txt(row, '이름', 20, 12, 0, PAT[pt.i].name, 19, { bold: true, color: PAT[pt.i].color });
    const st = INFO[pt.i].status, done = st === '완성';
    const chip = frame(row, '진행 상태', 90, 14, done ? 56 : 104, 24, done ? C.ok : C.empty, { r: 12, opacity: done ? 0.22 : 0.45 });
    txt(chip, '라벨', 0, 0, chip.width, st, 12, { h: 24, align: 'CENTER', bold: true, color: done ? C.ok : C.text });
    const more = button(row, '더보기 메뉴', PW - 32 - 48, 12, 36, 28, '···', { size: 16, r: 8, color: C.sub });
    if (pt.i === 1) menuAnchor = { x: 16 + PW - 32 - 48, y: ry + 12 + 28 };
    let ly = 48;
    for (const [k, v] of INFO[pt.i].lines) {
      txt(row, '항목', 20, ly, 76, k, 14, { color: C.sub });
      const t = txt(row, '값', 100, ly, PW - 32 - 120, v, 14);
      ly += t.height + 6;
    }
    if (INFO[pt.i].lines.length > 1) {
      rect(row, '구분선', 20, ly + 2, PW - 72, 1, C.line);
      const t = txt(row, '합계', 20, ly + 10, PW - 72, `합계  ${INFO[pt.i].total}`, 14, { bold: true });
      ly += t.height + 14;
    }
    row.resize(PW - 32, ly + 12);
    row.children[0].resize(6, ly + 12);
    ry += ly + 12 + 12;
  }
  boardCount(panel, 16, 760 - 48, PW - 32);
  // 방벽의 더보기 메뉴를 연 상태. 비우기는 메뉴 안에서만 실행한다.
  const menu = frame(s, '방벽 · 더보기 메뉴 (열림)', PX + menuAnchor.x - 236 + 36, 92 + menuAnchor.y + 6, 236, 10, C.panel2, { r: 10, stroke: C.line, shadow: true });
  button(menu, '격자에서 찾기', 8, 8, 220, 36, '격자에서 찾기', { size: 14, fill: C.panel2, r: 6 });
  const danger = frame(menu, '도안 비우기', 8, 48, 220, 10, C.dangerBg, { r: 6 });
  txt(danger, '라벨', 12, 8, 196, '도안 비우기…', 14, { bold: true, color: C.danger });
  const d = txt(danger, '설명', 12, 32, 196, '이번 턴 조각은 손패로, 이전 턴 조각은 묘지로 갑니다', 12, { color: C.sub });
  danger.resize(220, 32 + d.height + 10);
  menu.resize(236, 48 + danger.height + 8);

  placements(s, 40, 858, 4);
  playerStatus(s, 40, 902, { hp: 44, def: 0 });
  handRow(s, 420, 900, { lifted: 0, draw: 7, discard: 5 });
  endTurn(s, 1580, 896, 3, 6, 0);
}

// ---------- 안 C: 대치 구도 + 고정 미리보기 ----------
function tabC(p, name, x, y, w, color) {
  const tab = frame(p, name, x, y, w, 32, C.panel, { r: 8, stroke: color, sw: 2 });
  grip(tab, 8, 8, C.dim);
  txt(tab, '이름', 24, 0, w - 60, name.split(' ')[0], 15, { h: 32, bold: true, head: true, color });
  button(tab, '더보기 메뉴', w - 34, 4, 28, 24, '···', { size: 14, fill: C.panel2, r: 6, color: C.sub });
  return tab;
}
function buildC(s) {
  deco(s, 'screen');
  topBar(s, 6);
  const desk = deco(frame(s, '왼쪽 작업대', 32, 84, 860, 768, C.desk, { r: 18, stroke: C.line }), 'panel');
  txt(desk, '작업대 라벨', 24, 16, 0, '마법진 작업대', 15, { color: C.dim, bold: true, head: true });
  playerStatus(s, 56, 120, { hp: 31, def: 7, size: 104 });
  placements(s, 500, 132, 0, { note: '배치를 모두 사용했습니다 · 시전하거나 턴을 종료하세요' });
  const S = 120, gx = 102, gy = 300;
  drawGrid(s, gx, gy, S);
  for (const pt of BOARD.patterns) {
    const b = patternBox(pt), w = (b.x1 - b.x0) * S - 6, x = gx + b.x0 * S + 3, y = gy + b.y0 * S - 36;
    if (!kitInst('tab', PAT[pt.i].name, s, x, y)) tabC(s, `${PAT[pt.i].name} · 도안 탭`, x, y, w, PAT[pt.i].color);
  }
  castButton(s, gx, gy, S, BOARD.patterns[0], '시전');
  const aim = castButton(s, gx, gy, S, BOARD.patterns[2], '조준 중', { active: true, w: 128 });

  // 고정 미리보기: 선택하거나 조준 중인 도안의 계산식을 격자 아래 한 곳에서 보여준다.
  const pv = frame(s, '주문 미리보기 (고정)', gx, gy + 3 * S + 22, 6 * S, 10, C.panel, { r: 12, stroke: PAT[2].color, sw: 2 });
  rect(pv, '색 표식', 0, 0, 8, 10, PAT[2].color, { r: 4 });
  txt(pv, '제목', 24, 14, 0, '번개 · 조준 중', 17, { bold: true, head: true, color: PAT[2].color });
  txt(pv, '취소 안내', 6 * S - 230, 16, 210, 'Esc · 우클릭으로 취소', 13, { align: 'RIGHT', color: C.dim });
  const f1 = txt(pv, '계산식', 24, 46, 6 * S - 48, '기본 피해 12  +  불꽃 2  =  피해 14', 20, { bold: true });
  const f2 = txt(pv, '부가 효과', 24, 46 + f1.height + 6, 6 * S - 48, '공명 · 3칸 이상이라 시전 후 배치 +1    잔상 2장 · 시전 후 판에 1회 남음', 14, { color: C.sub });
  pv.resize(6 * S, 46 + f1.height + 6 + f2.height + 16);
  pv.children[0].resize(8, pv.height);
  deco(pv, 'preview');

  // 오른쪽 전장: 보스와 조준 중 예상 피해.
  const field = deco(frame(s, '오른쪽 전장', 912, 84, 976, 768, C.field, { r: 18, stroke: C.line }), 'panel');
  txt(field, '전장 라벨', 24, 16, 0, '전장', 15, { color: C.dim, bold: true, head: true });
  enemyUnit(s, 1400, 660, { name: '서고의 수호자', hp: 60, max: 60, intent: 12, pattern: '패턴 4 · 7 · 12 반복', w: 300, h: 380, color: C.enemy[2], boss: true, aim: true, preview: 14 });
  // 조준선은 도안이 없는 첫 줄 위로 우회해 유성의 빈 배치 구역을 가리지 않는다.
  const p0 = [aim.x + aim.width - 30, aim.y], p1 = [aim.x + aim.width + 30, gy + 30], p2 = [gx + 6 * S + 60, gy + 30], p3 = [1206, 430];
  const ox = Math.min(p0[0], p1[0], p2[0], p3[0]), oy = Math.min(p0[1], p1[1], p2[1], p3[1]);
  const rel = q => `${r2(q[0] - ox)} ${r2(q[1] - oy)}`;
  pathVec(s, '조준선', ox, oy, `M ${rel(p0)} C ${rel(p1)} ${rel(p2)} ${rel(p3)}`, { stroke: PAT[2].color, sw: 3, dash: [10, 8] });
  poly(s, '조준선 화살촉', [[p3[0] + 6, p3[1] + 6], [p3[0] - 12, p3[1] - 6], [p3[0] - 4, p3[1] + 12]], PAT[2].color);
  const chip = frame(s, '예상 피해', 1596, 330, 150, 56, C.panel, { r: 12, stroke: PAT[2].color, sw: 2, shadow: true });
  txt(chip, '수치', 0, 4, 150, '-14', 22, { align: 'CENTER', bold: true, color: PAT[2].color });
  txt(chip, '결과', 0, 32, 150, '체력 60 → 46', 12, { align: 'CENTER', color: C.sub });

  handRow(s, 32, 896, { dim: true, draw: 4, discard: 6 });
  boardCount(s, 1148, 952, 262);
  endTurn(s, 1580, 896, 4, 12, 7);
}

// ---------- 문서 영역 ----------
const COMMON = [
  '배치: 채운 다이아몬드와 빈 외곽선으로 구분하고 "2/3" 수치를 항상 표시한다. 추가 배치는 +N으로 붙인다.',
  '"제작 중" 수량은 "판 위 조각" 칩으로 격자 옆에 두고, 오류 메시지는 행동한 위치 근처의 토스트로 분리한다.',
  '도안 비우기는 ··· 메뉴 안으로 옮기고, 이전 턴 조각이 묘지로 간다는 결과를 메뉴 안에 적는다.',
  '적 공격 의도는 칼 아이콘과 큰 숫자로 머리 위에 둔다. 이름·체력 바는 한 묶음으로 실루엣 아래에 둔다.',
  '조작 설명은 드래그 중 커서 옆에만 띄운다. 상시 조작 안내 문구는 제거한다.',
  '도안 색은 금·은·연두·분홍으로 바꿔 특수 조각 색(주황·청·보라·청록)과 겹치지 않게 한다.',
  '드로우 덱·묘지는 카드 더미 모양과 수량으로 표시하고, 버튼과 모양을 구분한다.',
  '완성된 도안은 도안 위에 시전 버튼을 띄운다. 완성 도안에는 빈 구역이 없어 배치 영역을 가리지 않는다.',
  '도움말·메뉴는 우상단으로 옮겨 턴 종료와 떨어뜨린다. 빈 필수 구역 명도를 올리고 클릭 영역은 28px 이상으로 둔다.',
];
const VARIANTS = [
  { key: 'A', title: '수정안 A · 정돈된 3단', build: buildA, notes: [
    '현재 구조(적 → 마법진 → 손패)를 유지하고 정보 위계만 바로잡는 최소 변경안.',
    '도안 탭은 이름과 효과 요약 2줄. 긴 설명은 마우스를 올렸을 때 격자 밖 오른쪽 팝업에 항목별로 줄바꿈한다.',
    '상태 예시: 전투 4(거대 잉크 + 하수인), 배치 2/3, 손패 7장에서 회수를 시도해 토스트가 뜬 순간.',
  ] },
  { key: 'B', title: '수정안 B · 도안 원장', build: buildB, notes: [
    '오른쪽 고정 패널에 보유 도안 4개의 효과·진행·메뉴를 모은다. 설명이 길어지면 행이 세로로만 늘어난다.',
    '격자의 도안 탭은 이름만 남겨 격자를 넓게 쓴다. 원장 행에 마우스를 올리면 격자의 해당 도안을 강조한다.',
    '상태 예시: 적 3마리(1마리 쓰러짐), 공명으로 배치 4, 1/4 삼각형을 방벽 빈 구역에 드래그 중, 방벽 메뉴 열림.',
  ] },
  { key: 'C', title: '수정안 C · 대치 구도 + 고정 미리보기', build: buildC, notes: [
    '왼쪽은 플레이어 작업대(상태·배치·마법진), 오른쪽은 전장. 시전은 왼쪽에서 오른쪽으로 끌어 조준한다.',
    '격자 아래 고정 미리보기에 선택·조준 중인 도안의 계산식을 보여준다. 설명이 길어도 위치가 바뀌지 않는다.',
    '상태 예시: 보스전, 배치 0(손패 흐리게), 번개를 조준해 예상 피해 -14와 체력 60 → 46을 표시한 순간.',
  ] },
];

async function mainLayouts() {
  const target = await figma.getNodeByIdAsync(TARGET_ID);
  if (!target || !('appendChild' in target)) throw new Error('Frame 34(213:420)를 찾지 못했습니다');
  await figma.loadFontAsync({ family: FONT, style: 'Regular' });
  await figma.loadFontAsync({ family: FONT, style: 'Bold' });
  for (const old of target.children.filter(n => n.name === ROOT_NAME)) old.remove();
  let bottom = 0;
  for (const n of target.children) bottom = Math.max(bottom, n.y + n.height);
  const totalW = 3 * SW + 2 * GAP, PAD = 96;
  // 대상 프레임의 배경색과 관계없이 글자가 보이도록 어두운 바탕 위에 내용을 둔다.
  const outer = frame(target, ROOT_NAME, 80, bottom ? bottom + 120 : 80, totalW + PAD * 2, 100, '#0E1318', { r: 32 });
  const root = frame(outer, '내용', PAD, PAD, totalW, 100, null);

  txt(root, '문서 제목', 0, 0, 0, '주문 조각 · 전투 화면 레이아웃 수정안', 44, { bold: true });
  txt(root, '문서 부제', 0, 66, totalW, '기준 1920×1080 · 근거: design-critique 결과, PRD §11·§16, epic-idea 책 요약 · 세 안 모두 같은 도안 4개(불씨·번개 완성, 방벽·유성 미완성)를 사용해 비교한다.', 20, { color: C.sub });
  txt(root, '공통 수정 제목', 0, 120, 0, '세 안 공통 반영 사항', 24, { bold: true });
  let cy = 162;
  COMMON.forEach((line, i) => { const t = txt(root, `공통 ${i + 1}`, 0, cy, totalW, `${i + 1}.  ${line}`, 18, { color: C.text }); cy += t.height + 6; });

  const vy = cy + 80;
  let maxH = 0;
  VARIANTS.forEach((v, i) => {
    const col = frame(root, v.title, i * (SW + GAP), vy, SW, 100, null);
    txt(col, '안 제목', 0, 0, 0, v.title, 34, { bold: true });
    let ny = 56;
    for (const n of v.notes) { const t = txt(col, '설명', 0, ny, SW, '·  ' + n, 18, { color: C.sub }); ny += t.height + 4; }
    const screen = frame(col, `화면 ${v.key} · 1920×1080`, 0, ny + 24, SW, SH, C.bg, { clip: true });
    v.build(screen);
    col.resize(SW, ny + 24 + SH);
    maxH = Math.max(maxH, col.height);
  });
  root.resize(totalW, vy + maxH);
  outer.resize(totalW + PAD * 2, vy + maxH + PAD * 2);
  if (target.type === 'FRAME' || target.type === 'SECTION') {
    const needW = outer.x + outer.width + 80, needH = outer.y + outer.height + 80;
    if (target.width < needW || target.height < needH) {
      const w = Math.max(target.width, needW), h = Math.max(target.height, needH);
      if (target.type === 'SECTION') target.resizeWithoutConstraints(w, h); else target.resize(w, h);
    }
  }
  figma.viewport.scrollAndZoomIntoView([outer]);
  return outer;
}

// ================= 판타지 UI 키트 =================
function mix(a, b, t) {
  const x = hex(a), y = hex(b), h = v => Math.round(v * 255).toString(16).padStart(2, '0');
  return '#' + h(x.r + (y.r - x.r) * t) + h(x.g + (y.g - x.g) * t) + h(x.b + (y.b - x.b) * t);
}
function rgbHex(c) { const h = v => Math.round(v * 255).toString(16).padStart(2, '0'); return '#' + h(c.r) + h(c.g) + h(c.b); }
function strokeHex(n, fallback) { return n.strokes && n.strokes.length ? rgbHex(n.strokes[0].color) : fallback; }
// 모서리를 깎은 결정 형태. 모서리를 바탕색 삼각형으로 덮고 깎인 면에 선을 긋는다.
function chamfer(n, k, line) {
  const w = n.width, h = n.height, color = line || strokeHex(n, C.line), sw = n.strokeWeight || 1;
  n.cornerRadius = 0;
  const cuts = [
    [[[0, 0], [k, 0], [0, k]], [[k, 0], [0, k]]],
    [[[w, 0], [w - k, 0], [w, k]], [[w - k, 0], [w, k]]],
    [[[w, h], [w, h - k], [w - k, h]], [[w, h - k], [w - k, h]]],
    [[[0, h], [0, h - k], [k, h]], [[0, h - k], [k, h]]],
  ];
  for (const [tri, seg] of cuts) { poly(n, '장식 · 깎인 모서리', tri, C.bg); lines(n, '장식 · 깎인 면', [seg], color, sw); }
}
// 키트 1은 금색 안쪽 선만 쓴다. 마름모·별 장식은 2026-11-11 요청으로 제외했다.
const DECOR_ARCANE = {
  panel(n) {
    n.cornerRadius = 10;
    rect(n, '장식 · 금선', 8, 8, n.width - 16, n.height - 16, null, { r: 6, stroke: C.accent, strokeOpacity: 0.35, sw: 1 });
  },
  primary(n) {
    rect(n, '장식 · 금선', 5, 5, n.width - 10, n.height - 10, null, { r: Math.max(0, (n.cornerRadius || 0) - 4), stroke: C.accent, strokeOpacity: 0.7, sw: 1 });
  },
};
const DECOR_PARCHMENT = {
  panel(n) {
    n.cornerRadius = 4;
    rect(n, '장식 · 안쪽 괘선', 6, 6, n.width - 12, n.height - 12, null, { r: 2, stroke: C.line, strokeOpacity: 0.9, sw: 1 });
    for (const [x, y] of [[6, 6], [n.width - 6, 6], [n.width - 6, n.height - 6], [6, n.height - 6]]) ellipse(n, '장식 · 모서리 점', x - 4, y - 4, 8, 8, C.accent);
  },
  primary(n) {
    n.cornerRadius = 6;
    rect(n, '장식 · 안쪽 괘선', 4, 4, n.width - 8, n.height - 8, null, { r: 3, stroke: C.primaryText, strokeOpacity: 0.35, sw: 1 });
    const d = Math.min(36, n.height - 16), x = 12, y = (n.height - d) / 2;
    ellipse(n, '장식 · 봉랍', x, y, d, d, '#5A1E1B', { stroke: '#3E1412', sw: 2 });
    const cx = x + d / 2, cy = y + d / 2, R = d * 0.32, r = d * 0.13, pts = [];
    for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? r : R; pts.push([cx + Math.cos(a) * rr, cy + Math.sin(a) * rr]); }
    poly(n, '장식 · 봉랍 별', pts, '#E8C9A0');
  },
  card(n) { n.cornerRadius = 6; rect(n, '장식 · 안쪽 괘선', 4, 4, n.width - 8, n.height - 8, null, { r: 3, stroke: C.line, strokeOpacity: 0.7, sw: 1 }); },
};
const DECOR_CRYSTAL = {
  screen(n) {
    lines(n, '장식 · 결정 면', [[[0, 260], [520, 0]], [[1400, 1080], [1920, 640]], [[1500, 0], [1920, 300]]], C.accent, 1, { strokeOpacity: 0.06 });
  },
  panel(n) { chamfer(n, 22); lines(n, '장식 · 광택', [[[30, 2], [n.width * 0.45, 2]]], C.accent, 2, { strokeOpacity: 0.5 }); },
  preview(n) { chamfer(n, 12); },
  primary(n) { chamfer(n, 14, C.accent); lines(n, '장식 · 광택', [[[20, 3], [n.width * 0.5, 3]]], C.accent, 2, { strokeOpacity: 0.6 }); },
  card(n) { chamfer(n, 10); },
};
const THEMES = [
  {
    key: '1', name: '별빛 마도서', heads: ['Noto Serif KR', 'Nanum Myeongjo'], decor: DECOR_ARCANE,
    concept: '깊은 남색 밤하늘과 금박 장식선. 오래된 마도서 표지처럼 차분하고 정돈된 마법.',
    colors: {
      bg: '#0F1426', panel: '#171E35', panel2: '#202946', line: '#39446A', text: '#F1EBDD', sub: '#BDB4A0', dim: '#857F72', ink: '#141017',
      cellBg: '#141A30', cellLine: '#2A3356', empty: '#5D6C98', primary: '#2B3A6E', primaryText: '#F6E7C1', accent: '#D6B46E',
      desk: '#131A2E', field: '#151C33', portrait: '#202946', pileBack1: '#1A2240', pileBack2: '#1E2748', pileFront: '#232D52', pileLine: '#D6B46E',
      intentBg: '#3A1F2A', warnBg: '#3A3020', dangerBg: '#3A1F2A',
    },
  },
  {
    key: '2', name: '양피지 서재', heads: ['Gowun Batang', 'Noto Serif KR'], decor: DECOR_PARCHMENT,
    concept: '밝은 양피지와 잉크, 봉랍. 책상 위에 주문서를 펼친 따뜻한 낮의 서재.',
    colors: {
      bg: '#E8DCC2', panel: '#F5EDDC', panel2: '#EDE2CB', line: '#B8A47E', text: '#2F2418', sub: '#5F4E39', dim: '#8A785F', ink: '#2F2418',
      cellBg: '#EFE5CF', cellLine: '#CBB994', empty: '#8F7A57', normal: '#6F7C88',
      hp: '#B4473C', hpTrack: '#7D6A55', hpPlayer: '#4E8A4F', hpPlayerTrack: '#6E7A5E', intent: '#A63B33', intentBg: '#F3DCD2', def: '#3E6E9C',
      ok: '#3F8E5F', warn: '#9A6A12', danger: '#A63B33', primary: '#7C2F2A', primaryText: '#F8EEDC', accent: '#7C2F2A',
      desk: '#F1E7D3', field: '#EFE4CD', portrait: '#E2D3B4', pileBack1: '#E0D2B6', pileBack2: '#E6D9BF', pileFront: '#F3EAD7', pileLine: '#8A6F48',
      warnBg: '#F4E5C4', dangerBg: '#F3DCD2',
    },
    pat: [{ name: '불씨', color: '#C2902A' }, { name: '방벽', color: '#66727F' }, { name: '번개', color: '#5E8F24' }, { name: '유성', color: '#BC4C7B' }],
  },
  {
    key: '3', name: '월장석 결정', heads: ['IBM Plex Sans KR', 'Noto Sans KR'], decor: DECOR_CRYSTAL,
    concept: '보랏빛 어둠 속 월장석 결정. 모서리를 깎은 면과 은은한 광택으로 날렵하고 현대적인 마법.',
    colors: {
      bg: '#13131C', panel: '#1C1C29', panel2: '#262638', line: '#3C3C58', text: '#EEEDF7', sub: '#B3B0CA', dim: '#7A7793', ink: '#13131C',
      cellBg: '#181824', cellLine: '#2E2E44', empty: '#5F5F8A', primary: '#3B3B63', primaryText: '#EEF4FF', accent: '#CFE1F6',
      desk: '#171723', field: '#191926', portrait: '#262638', pileBack1: '#1E1E2D', pileBack2: '#222233', pileFront: '#28283C', pileLine: '#CFE1F6',
      intentBg: '#33202A', warnBg: '#332C1E', dangerBg: '#33202A',
    },
  },
];
const KIT_CARDS = [
  { shape: 'quarter', name: '1/4 삼각형' },
  { shape: 'half', name: '반칸 삼각형' },
  { shape: 'square', name: '정사각형' },
  { shape: 'rect', name: '직사각형' },
  { shape: 'half', name: '불꽃 삼각형', fx: 'flame', fxText: '공격 피해 +2' },
  { shape: 'square', name: '수호 정사각형', fx: 'guard', fxText: '방어 +3' },
  { shape: 'rect', name: '공명 직사각형', fx: 'resonance', fxText: '3칸 이상 배치 +1' },
  { shape: 'quarter', name: '잔상 1/4 삼각형', fx: 'afterimage', fxText: '시전 후 1회 유지' },
];

function comp(p, name, w, h) { const n = add(p, figma.createComponent(), name); n.resize(w, h); n.fills = []; n.clipsContent = false; return n; }
// 변형 명세로 컴포넌트를 만들고 하나의 컴포넌트 세트로 묶은 뒤 격자로 정렬한다.
function variantSet(p, title, desc, specs, cols) {
  const comps = specs.map(sp => { const c = comp(p, sp.props, sp.w, sp.h); sp.draw(c); return c; });
  const set = figma.combineAsVariants(comps, p);
  set.name = title;
  set.description = desc;
  const pad = 24, gap = 20;
  let x = pad, y = pad, rowH = 0, maxW = 0;
  comps.forEach((c, i) => {
    if (i > 0 && i % cols === 0) { x = pad; y += rowH + gap; rowH = 0; }
    c.x = x; c.y = y; x += c.width + gap; rowH = Math.max(rowH, c.height); maxW = Math.max(maxW, x - gap);
  });
  set.resizeWithoutConstraints(maxW + pad, y + rowH + pad);
  set.fills = [];
  set.strokes = paint(C.line); set.strokeWeight = 1; set.dashPattern = [6, 4]; set.cornerRadius = 12;
  const map = {};
  specs.forEach((sp, i) => { map[sp.key] = comps[i]; });
  return { node: set, map };
}
function singleComp(p, title, desc, w, h, draw) {
  const c = comp(p, title, w, h); draw(c); c.description = desc;
  return { node: c, map: { '기본': c } };
}
function kitBlock(p, title, desc, build) {
  const b = frame(p, title, 0, 0, 10, 10, null);
  const res = build(b);
  const t = txt(b, '블록 제목', 0, 0, 0, title, 18, { bold: true, head: true });
  const d = txt(b, '블록 설명', 0, t.height + 4, Math.max(res.node.width, 300), desc, 13, { color: C.sub });
  res.node.x = 0; res.node.y = t.height + 4 + d.height + 12;
  b.resize(Math.max(res.node.width, 300), res.node.y + res.node.height);
  return { block: b, map: res.map };
}
function swatch(p, x, y, color, name, code) {
  rect(p, `색 · ${name}`, x, y, 140, 72, color, { r: 10, stroke: C.line });
  txt(p, '이름', x, y + 80, 140, name, 14, { bold: true });
  txt(p, '값', x, y + 102, 140, code || color, 12, { color: C.dim });
}
function section(p, y, title, note) {
  txt(p, '구역 제목', 0, y, 0, title, 30, { bold: true, head: true });
  if (note) txt(p, '구역 설명', 0, y + 48, SW, note, 15, { color: C.sub });
  rect(p, '구역 선', 0, y + (note ? 80 : 52), SW, 1, C.accent, { opacity: 0.5 });
  return y + (note ? 108 : 80);
}
function gridCellFace(p, state) {
  const S = 96, g = frame(p, '격자 칸', 0, 0, S, S, null);
  rect(g, '칸', 0, 0, S, S, C.cellBg, { stroke: C.cellLine });
  const sec = s => sectorPts(0, 0, s, S);
  if (state === '필수 구역') vec(g, '빈 필수 구역', [0, 1, 2, 3].map(sec), C.empty);
  if (state === '일반 조각') vec(g, '조각', [0, 1, 2, 3].map(sec), C.normal, { stroke: C.normal, sw: 1 });
  if (state === '배치 가능') { vec(g, '빈 필수 구역', [2, 3].map(sec), C.empty); vec(g, '미리보기 · 가능', [0, 1].map(sec), C.ok, { opacity: 0.85 }); }
  if (state === '배치 불가') { vec(g, '조각', [0, 1].map(sec), C.normal); vec(g, '미리보기 · 불가', [1, 2].map(sec), C.danger, { opacity: 0.8 }); }
  const c = corners(0, 0, S);
  lines(g, '구역 구분선', [[c[0], c[2]], [c[1], c[3]]], C.cellLine, 1);
  if (state === '배치 가능' || state === '필수 구역') rect(g, '도안 외곽선', 0, 0, S, S, null, { stroke: PAT[2].color, sw: 4 });
  return g;
}
function hpBarFace(p, kind) {
  const g = frame(p, '체력 바', 0, 0, 300, 26, null);
  const player = kind === '플레이어', ratio = player ? 31 / 50 : 1;
  rect(g, '바탕', 0, 0, 300, 26, player ? C.hpPlayerTrack : C.hpTrack, { r: 6 });
  rect(g, '현재', 0, 0, 300 * (kind === '적 · 예상 피해' ? 46 / 60 : ratio), 26, player ? C.hpPlayer : C.hp, { r: 6 });
  if (kind === '적 · 예상 피해') rect(g, '예상 피해', 300 * 46 / 60, 0, 300 * 14 / 60, 26, '#FFFFFF', { opacity: 0.55 });
  txt(g, '체력 수치', 0, 0, 300, player ? '체력 31 / 50' : kind === '적' ? '60 / 60' : '60 → 46 / 60', 15, { h: 26, align: 'CENTER', bold: true, color: C.barText });
  return g;
}
function panelFace(p, kind) {
  const fill = kind === '작업대' ? C.desk : kind === '전장' ? C.field : C.panel;
  const f = frame(p, `패널 · ${kind}`, 0, 0, 360, 180, fill, { r: 18, stroke: kind === '미리보기' ? PAT[2].color : C.line, sw: kind === '미리보기' ? 2 : 1 });
  deco(f, kind === '미리보기' ? 'preview' : 'panel');
  txt(f, '라벨', 28, 26, 0, kind === '미리보기' ? '번개 · 조준 중' : kind === '작업대' ? '마법진 작업대' : '전장', 17, { bold: true, head: true, color: kind === '미리보기' ? PAT[2].color : C.sub });
  return f;
}
function menuFace(p) {
  const menu = frame(p, '더보기 메뉴', 0, 0, 236, 10, C.panel2, { r: 10, stroke: C.line, shadow: true });
  button(menu, '격자에서 찾기', 8, 8, 220, 36, '격자에서 찾기', { size: 14, fill: C.panel2, r: 6 });
  const danger = frame(menu, '도안 비우기', 8, 48, 220, 10, C.dangerBg, { r: 6 });
  txt(danger, '라벨', 12, 8, 196, '도안 비우기…', 14, { bold: true, color: C.danger });
  const d = txt(danger, '설명', 12, 32, 196, '이번 턴 조각은 손패로, 이전 턴 조각은 묘지로 갑니다', 12, { color: C.sub });
  danger.resize(220, 32 + d.height + 10);
  menu.resize(236, 48 + danger.height + 8);
  return menu;
}

function buildComponents(p, y0) {
  const kit = {}, blocks = [];
  const push = (key, r) => { blocks.push(r.block); if (key) kit[key] = r.map; };
  const kinds = [['주요', '확인'], ['보조', '취소'], ['위험', '도안 비우기']], states = ['기본', '호버', '비활성'];
  push(null, kitBlock(p, '버튼', '종류 × 상태. 주요는 화면당 하나, 위험은 메뉴 안에서만 쓴다.', b => variantSet(b, '버튼', '일반 버튼', kinds.flatMap(([k, label]) => states.map(st => ({
    props: `종류=${k}, 상태=${st}`, key: `${k}|${st}`, w: 220, h: 56, draw: c => {
      const base = k === '주요' ? C.primary : k === '위험' ? C.dangerBg : C.panel2;
      const fill = st === '호버' ? mix(base, C.text, 0.12) : base;
      const btn = button(c, '버튼', 0, 0, 220, 56, label, { size: 17, bold: true, fill, r: 12, color: k === '주요' ? C.primaryText : k === '위험' ? C.danger : C.text, stroke: k === '보조' ? C.line : k === '위험' ? C.danger : undefined });
      if (k === '주요') deco(btn, 'primary');
      if (st === '비활성') c.opacity = 0.4;
    },
  }))), 3)));
  push('endTurn', kitBlock(p, '턴 종료 버튼', '우하단 고정. 라벨에 현재 턴을 넣는다.', b => singleComp(b, '턴 종료 버튼', '턴 종료', 300, 88, c => endTurnFace(c, 0, 0, '4턴 종료'))));
  push('cast', kitBlock(p, '시전 버튼', '완성된 도안 위에 뜬다. 도안 색을 그대로 쓴다.', b => variantSet(b, '시전 버튼', '도안 위 시전', PAT.flatMap(pt => ['기본', '조준 중'].map(st => ({
    props: `도안=${pt.name}, 상태=${st}`, key: `${pt.name}|${st}`, w: st === '기본' ? 104 : 128, h: 44,
    draw: c => castFace(c, '시전 버튼', 0, 0, st === '기본' ? 104 : 128, st === '기본' ? '시전' : '조준 중', pt.color, st !== '기본'),
  }))), 4)));
  push('tab', kitBlock(p, '도안 탭', '도안 크기에 맞춘 너비. 이동 핸들과 더보기 메뉴를 포함한다.', b => variantSet(b, '도안 탭', '도안 이름 탭', PAT.map((pt, i) => ({
    props: `도안=${pt.name}`, key: pt.name, w: i < 2 ? 114 : 234, h: 32, draw: c => tabC(c, `${pt.name} · 도안 탭`, 0, 0, i < 2 ? 114 : 234, pt.color),
  })), 4)));
  push('card', kitBlock(p, '손패 카드', '실제 칸 비율의 조각 모양과 특수 효과. 배치 불가 상태는 흐리게 표시한다.', b => variantSet(b, '손패 카드', '손패 조각', ['기본', '배치 불가'].flatMap(st => KIT_CARDS.map(cd => ({
    props: `종류=${cd.name}, 상태=${st}`, key: `${cd.name}|${st}`, w: 104, h: 150, draw: c => handCard(c, 0, 0, cd, { dim: st === '배치 불가' }),
  }))), 8)));
  push('pile', kitBlock(p, '더미', '드로우 덱과 묘지. 수량 글자를 바꿔 쓴다.', b => variantSet(b, '더미', '카드 더미', ['드로우 덱', '묘지'].map(l => ({
    props: `종류=${l}`, key: l, w: 104, h: 150, draw: c => pile(c, 0, 0, l, l === '묘지' ? 6 : 4),
  })), 2)));
  push('placement', kitBlock(p, '남은 배치', '채운 마름모와 빈 외곽선, 수치를 함께 표시한다.', b => variantSet(b, '남은 배치', '배치 횟수', [0, 1, 2, 3, 4].map(n => ({
    props: `남은=${n}`, key: String(n), w: 240, h: 30, draw: c => placementFace(c, 0, 0, n),
  })), 1)));
  push('intent', kitBlock(p, '공격 의도', '적 머리 위. 수치 글자를 바꿔 쓴다.', b => singleComp(b, '공격 의도', '적 공격 의도', 112, 48, c => intentBadge(c, 0, 0, 12))));
  push(null, kitBlock(p, '체력 바', '플레이어·적·조준 중 예상 피해.', b => variantSet(b, '체력 바', '체력 바', ['플레이어', '적', '적 · 예상 피해'].map(k => ({
    props: `대상=${k}`, key: k, w: 300, h: 26, draw: c => hpBarFace(c, k),
  })), 1)));
  push(null, kitBlock(p, '격자 칸', '빈 칸, 도안의 빈 필수 구역, 조각, 배치 미리보기.', b => variantSet(b, '격자 칸', '마법진 칸', ['빈 칸', '필수 구역', '일반 조각', '배치 가능', '배치 불가'].map(st => ({
    props: `상태=${st}`, key: st, w: 96, h: 96, draw: c => gridCellFace(c, st),
  })), 5)));
  push('chip', kitBlock(p, '판 위 조각', '격자 옆 상태 칩.', b => singleComp(b, '판 위 조각', '판 위 조각 수', 262, 32, c => boardCount(c, 0, 0, 262))));
  push(null, kitBlock(p, '토스트', '행동한 위치 근처에 잠깐 표시한다.', b => singleComp(b, '토스트', '경고 토스트', 470, 40, c => toast(c, 235, 0, '손패가 7장이라 조각을 회수할 수 없습니다'))));
  push(null, kitBlock(p, '패널', '작업대·전장·고정 미리보기의 바탕.', b => variantSet(b, '패널', '패널', ['작업대', '전장', '미리보기'].map(k => ({
    props: `종류=${k}`, key: k, w: 360, h: 180, draw: c => panelFace(c, k),
  })), 3)));
  push(null, kitBlock(p, '더보기 메뉴', '도안 비우기는 이 메뉴 안에서만 실행한다.', b => singleComp(b, '더보기 메뉴', '도안 메뉴', 236, 128, c => menuFace(c))));
  // 블록을 왼쪽부터 채우고 폭을 넘으면 다음 줄로 넘긴다.
  let x = 0, y = y0, rowH = 0;
  for (const b of blocks) {
    if (x > 0 && x + b.width > SW) { x = 0; y += rowH + 56; rowH = 0; }
    b.x = x; b.y = y; x += b.width + 56; rowH = Math.max(rowH, b.height);
  }
  return { kit, bottom: y + rowH };
}

function buildKitSheet(p, theme, x, y) {
  const KW = SW + 160;
  const kf = frame(p, `키트 ${theme.key} · ${theme.name}`, x, y, KW, 100, C.bg, { r: 32, stroke: C.line });
  const body = frame(kf, '내용', 80, 80, SW, 100, null);
  txt(body, '키트 제목', 0, 0, 0, `키트 ${theme.key} · ${theme.name}`, 52, { bold: true, head: true });
  txt(body, '콘셉트', 0, 82, SW, theme.concept, 20, { color: C.sub });
  txt(body, '글꼴', 0, 122, 0, `제목 ${HEAD} · 본문 ${FONT}`, 16, { color: C.dim });
  let cy = section(body, 190, '색상', '화면 꾸밈 색은 테마마다 바뀌고, 게임 의미 색(도안·특수 조각·체력·의도)은 의미를 유지한다. 밝은 테마는 같은 색상군에서 명도만 낮춘다.');
  const chrome = [['bg', '배경'], ['panel', '패널'], ['panel2', '패널 강조'], ['line', '선'], ['text', '글자'], ['sub', '보조 글자'], ['accent', '장식'], ['primary', '주요 버튼'], ['danger', '위험'], ['warn', '경고'], ['ok', '가능']];
  chrome.forEach(([k, n], i) => swatch(body, i * 168, cy, C[k], n));
  cy += 140;
  txt(body, '게임 의미 색', 0, cy, 0, '게임 의미 색', 16, { bold: true, color: C.sub });
  cy += 32;
  const game = PAT.map(pt => [pt.color, `도안 · ${pt.name}`]).concat([[C.normal, '일반 조각'], [C.flame, '불꽃'], [C.guard, '수호'], [C.resonance, '공명'], [C.afterimage, '잔상'], [C.hp, '적 체력'], [C.intent, '공격 의도'], [C.def, '방어']]);
  game.forEach(([c, n], i) => swatch(body, i * 158, cy, c, n));
  cy += 176;

  cy = section(body, cy, '글자');
  const types = [['제목 1', 44, true, true, '주문 조각'], ['제목 2', 26, true, true, '서고의 수호자 · 번개 · 조준 중'], ['수치', 28, true, false, '60 → 46   피해 14'], ['본문', 16, false, false, '선택한 적에게 피해 12 · 공명으로 시전 후 배치 +1'], ['보조', 13, false, false, 'Esc · 우클릭으로 취소   클릭해 보기']];
  for (const [label, size, bold, head, sample] of types) {
    txt(body, '단계', 0, cy + 4, 0, label, 15, { bold: true, color: C.sub });
    txt(body, '규격', 0, cy + 28, 0, `${head ? HEAD : FONT} ${bold ? 'Bold' : 'Regular'} ${size}`, 12, { color: C.dim });
    const t = txt(body, '예시', 280, cy, SW - 280, sample, size, { bold, head });
    cy += Math.max(t.height, 52) + 20;
  }
  cy += 20;

  cy = section(body, cy, '컴포넌트', '모두 Figma 컴포넌트·변형으로 만들었다. 아래 C안 예시는 이 컴포넌트의 인스턴스로 조립했다.');
  const built = buildComponents(body, cy);
  cy = built.bottom + 80;

  cy = section(body, cy, 'C안 적용 예시 · 1920×1080');
  const screen = frame(body, `키트 ${theme.key} · C안 적용 예시`, 0, cy, SW, SH, C.bg, { clip: true, stroke: C.line, r: 8 });
  KIT = built.kit;
  buildC(screen);
  KIT = null;
  body.resize(SW, cy + SH);
  kf.resize(KW, cy + SH + 160);
  return kf;
}

async function pickFont(available, candidates) {
  for (const fam of candidates) {
    if (available.some(f => f.fontName.family === fam && f.fontName.style === 'Regular') && available.some(f => f.fontName.family === fam && f.fontName.style === 'Bold')) {
      await figma.loadFontAsync({ family: fam, style: 'Regular' });
      await figma.loadFontAsync({ family: fam, style: 'Bold' });
      return fam;
    }
  }
  return BASE_FONT;
}

async function mainKits() {
  const target = await figma.getNodeByIdAsync(TARGET_ID);
  if (!target) throw new Error('Frame 34(213:420)를 찾지 못했습니다');
  let page = target; while (page && page.type !== 'PAGE') page = page.parent;
  await figma.setCurrentPageAsync(page);
  await figma.loadFontAsync({ family: BASE_FONT, style: 'Regular' });
  await figma.loadFontAsync({ family: BASE_FONT, style: 'Bold' });
  const available = await figma.listAvailableFontsAsync();
  for (const old of page.children.filter(n => n.name === KIT_ROOT_NAME)) old.remove();
  let right = 0, top = 0;
  for (const n of page.children) { right = Math.max(right, n.x + n.width); }
  let anchor = target; while (anchor.parent && anchor.parent.type !== 'PAGE') anchor = anchor.parent;
  top = anchor.y;
  const root = frame(page, KIT_ROOT_NAME, right + 400, top, 100, 100, '#0B0E13', { r: 40 });
  C = { ...BASE }; FONT = BASE_FONT; HEAD = BASE_FONT;
  txt(root, '문서 제목', 96, 80, 0, '주문 조각 · 판타지/마법 UI 키트 3안', 56, { bold: true });
  txt(root, '문서 부제', 96, 160, 0, 'C안(대치 구도 + 고정 미리보기) 기준 · 각 키트는 색상, 글자, 컴포넌트, 적용 예시로 구성된다.', 22, { color: C.sub });
  let x = 96, maxH = 0;
  for (const theme of THEMES) {
    C = { ...BASE, ...theme.colors };
    PAT = theme.pat || BASE_PAT;
    DECOR = theme.decor;
    HEAD = await pickFont(available, theme.heads);
    const kf = buildKitSheet(root, theme, x, 260);
    x += kf.width + 120;
    maxH = Math.max(maxH, kf.height);
  }
  C = { ...BASE }; PAT = BASE_PAT; DECOR = null; HEAD = BASE_FONT;
  root.resize(x - 120 + 96, 260 + maxH + 96);
  figma.viewport.scrollAndZoomIntoView([root]);
  return root;
}

const COMMAND = figma.command || 'layouts';
(COMMAND === 'kits' ? mainKits() : mainLayouts())
  .then(() => figma.closePlugin(COMMAND === 'kits' ? '판타지 UI 키트 3안을 Frame 34 오른쪽에 만들었습니다' : '전투 UI 레이아웃 수정안 3개를 Frame 34에 만들었습니다'))
  .catch(e => figma.closePlugin('오류: ' + e.message));
