const target = await figma.getNodeByIdAsync('193:1456');
if (!target || target.type !== 'FRAME') throw new Error('Frame 33 is unavailable');
const start = __START__, end = __END__;
const created = [];
const color = h => ({r:parseInt(h.slice(1,3),16)/255,g:parseInt(h.slice(3,5),16)/255,b:parseInt(h.slice(5,7),16)/255});
const solid = h => [{type:'SOLID',color:color(h)}];
await figma.loadFontAsync({family:'Noto Sans KR',style:'Regular'});
await figma.loadFontAsync({family:'Noto Sans KR',style:'Bold'});
function add(parent,node,name){ parent.appendChild(node); node.name=name; created.push(node.id); return node; }
function frame(parent,name,x,y,w,h,fill,rad=0){const n=add(parent,figma.createFrame(),name);n.resize(w,h);n.x=x;n.y=y;n.fills=fill?solid(fill):[];n.cornerRadius=rad;n.clipsContent=false;return n;}
function rect(parent,name,x,y,w,h,fill,rad=0,stroke=null,sw=1){const n=add(parent,figma.createRectangle(),name);n.resize(w,h);n.x=x;n.y=y;n.fills=fill?solid(fill):[];n.cornerRadius=rad;if(stroke){n.strokes=solid(stroke);n.strokeWeight=sw;}return n;}
function ellipse(parent,name,x,y,w,h,fill){const n=add(parent,figma.createEllipse(),name);n.resize(w,h);n.x=x;n.y=y;n.fills=solid(fill);return n;}
function vector(parent,name,x,y,path,fill,stroke=null,sw=1){const n=add(parent,figma.createVector(),name);n.vectorPaths=[{windingRule:'NONZERO',data:path}];n.x=x;n.y=y;n.fills=fill?solid(fill):[];if(stroke){n.strokes=solid(stroke);n.strokeWeight=sw;}return n;}
function txt(parent,name,x,y,w,h,value,size,fill='#E9F2EE',bold=false){const n=add(parent,figma.createText(),name);n.fontName={family:'Noto Sans KR',style:bold?'Bold':'Regular'};n.fontSize=size;n.characters=value;n.textAutoResize='NONE';n.resize(w,h);n.x=x;n.y=y;n.fills=solid(fill);return n;}
function instance(parent,component,name,x,y,width){const n=add(parent,component.createInstance(),name);n.rescale(width/component.width);n.x=x;n.y=y;return n;}
let bank;
if(start===0){
  if(target.findOne(n=>n.name==='전투 UI 시안 에셋')) throw new Error('Existing concept assets found; inspect before retry');
  bank=frame(target,'전투 UI 시안 에셋',20,6300,900,300,null);bank.visible=false;
  const monsterColors=['#8B778C','#718D7E','#AB907B'];
  for(let i=0;i<3;i++){
    const c=add(bank,figma.createComponent(),'몬스터 '+(i+1));c.resize(150,180);c.x=i*170;c.fills=[];
    const body=monsterColors[i];
    ellipse(c,'몸체',8,44,134,120,body);
    vector(c,'왼쪽 뿔',13,22,'M 0 0 L 39 38 L 8 48 Z',body);
    vector(c,'오른쪽 뿔',98,22,'M 39 0 L 31 48 L 0 38 Z',body);
    if(i===1){ellipse(c,'얼굴',34,59,82,79,'#465D56');ellipse(c,'눈',60,82,30,25,'#F2E8CB');ellipse(c,'동공',72,89,8,11,'#263339');}
    else {ellipse(c,'왼눈',35,83,24,18,'#F2E8CB');ellipse(c,'오른눈',91,83,24,18,'#F2E8CB');ellipse(c,'동공 L',44,88,7,8,'#283339');ellipse(c,'동공 R',100,88,7,8,'#283339');}
    rect(c,'입',59,122,32,5,'#37434A',2);
  }
  for(let i=0;i<3;i++){
    const c=add(bank,figma.createComponent(),['마법사 측면','마법사 뒷모습','마법사 초상화'][i]);c.resize(150,190);c.x=520+i*160;c.fills=[];
    ellipse(c,'후드',24,5,102,95,'#748D98');ellipse(c,'얼굴',42,25,66,65,i===1?'#405C68':'#D5C4A6');
    vector(c,'망토',8,86,'M 36 0 L 98 0 L 134 98 L 0 98 Z','#5C7886');
    if(i!==1){ellipse(c,'눈',78,52,7,7,'#26363E');}
    rect(c,'띠',30,145,90,10,'#C3A471',3);
  }
} else { bank=target.findOne(n=>n.name==='전투 UI 시안 에셋'); if(!bank)throw new Error('Concept assets are missing'); }
const parts=bank.children;
const m=parts.filter(n=>n.type==='COMPONENT'&&n.name.startsWith('몬스터'));
const h=parts.filter(n=>n.type==='COMPONENT'&&n.name.startsWith('마법사'));
if(m.length!==3||h.length!==3)throw new Error('Concept assets are incomplete');
const variants=[
 {name:'측면 결투',note:'측면 전장 · 손패 → 인접 도안 → 시전 → 오른쪽 적',scene:[25,65,1550,335],board:[30,420,690,260],spells:[740,420,800,260],hand:[30,700,1510,150],mode:'side'},
 {name:'후방 대각선',note:'플레이어 뒷모습 · 대각선으로 전장과 적을 바라보는 구도',scene:[25,65,1550,500],board:[230,440,670,250],spells:[920,465,590,225],hand:[230,715,1280,140],mode:'back'},
 {name:'던전 정면',note:'왼쪽 초상화 · 정면 몬스터의 체력과 의도를 동일 높이로 비교',scene:[25,65,1550,345],board:[245,430,720,250],spells:[985,430,555,250],hand:[245,710,1295,140],mode:'portrait'},
 {name:'마법사의 책상',note:'손패 · 도안 · 시전 순서가 좌에서 우로 흐르는 작업대',scene:[25,65,1550,290],board:[445,380,630,340],spells:[1095,380,445,340],hand:[30,380,395,455],mode:'side'},
 {name:'왼쪽 주문서',note:'왼쪽에 제작 동선을 모으고 오른쪽에 전장과 대상을 배치',scene:[810,70,730,570],board:[30,75,735,265],spells:[30,360,735,255],hand:[30,660,1510,165],mode:'side'},
 {name:'세로 적 진형',note:'중앙 도안에서 가까운 오른쪽 적 진형으로 시전',scene:[1120,70,420,640],board:[30,80,640,300],spells:[690,80,395,300],hand:[30,410,1055,210],mode:'vertical'},
 {name:'전술 탁상',note:'위에서 내려다보는 전장 · 도안과 적을 한 화면 중심에 배치',scene:[25,65,1550,550],board:[470,315,650,255],spells:[1140,340,365,230],hand:[240,640,1160,170],mode:'tactical'},
 {name:'펼친 마도서',note:'왼쪽 책장에 도안 · 오른쪽 책장에 몬스터와 주문',scene:[805,75,735,290],board:[30,75,740,525],spells:[805,385,735,215],hand:[30,630,1510,180],mode:'portrait'},
 {name:'전장 중심',note:'넓은 전장 위에 도안과 주문을 띄워 짧게 이동',scene:[25,65,1550,545],board:[360,365,620,250],spells:[1000,385,500,225],hand:[30,650,1510,160],mode:'side'},
 {name:'대상별 전선',note:'세 적의 체력과 의도를 나란히 정렬 · 손패는 왼쪽 세로 배치',scene:[25,65,1550,290],board:[410,385,690,325],spells:[1120,385,420,325],hand:[30,385,360,430],mode:'lanes'}
];
function makeScene(screen,cfg,s){
 const [x,y,w,H]=cfg.scene;const scene=frame(screen,'전장',x*s,y*s,w*s,H*s,'#20343C',12*s);
 rect(scene,'지면',0,H*s*.73,w*s,H*s*.27,'#35443F',0);
 txt(scene,'전장 구도',20*s,12*s,500*s,30*s,cfg.mode==='back'?'후방 대각선 시점':cfg.mode==='portrait'?'플레이어 초상화 · 적 정면':cfg.mode==='vertical'?'세로 적 진형':cfg.mode==='tactical'?'전술 탁상':cfg.mode==='lanes'?'대상별 전선':'측면 전장',17*s,'#A7BEB9');
 if(cfg.mode==='vertical'){
   instance(scene,h[0],'플레이어',270*s,420*s,100*s);txt(scene,'플레이어 체력',250*s,545*s,160*s,30*s,'마법사 50/50',15*s,'#B8E1D5',true);
   for(let i=0;i<3;i++){let yy=40+i*165;instance(scene,m[i],'적 '+(i+1),35*s,yy*s,90*s);txt(scene,'적 의도',145*s,(yy+25)*s,230*s,26*s,'공격 4',17*s,'#F3B69F',true);txt(scene,'적 체력',145*s,(yy+56)*s,230*s,26*s,'잉크 '+String.fromCharCode(65+i)+'   12/12',15*s);rect(scene,'체력 트랙',145*s,(yy+88)*s,165*s,7*s,'#52605B',3*s);rect(scene,'체력',145*s,(yy+88)*s,165*s,7*s,'#DD8877',3*s);}
 }else{
   const isWide=w>1000;const heroW=cfg.mode==='back'?155:cfg.mode==='portrait'?105:125;const heroX=30,heroY=Math.max(48,H-210);
   instance(scene,h[cfg.mode==='back'?1:cfg.mode==='portrait'?2:0],'플레이어',heroX*s,heroY*s,heroW*s);
   txt(scene,'플레이어 체력',(heroX+2)*s,(H-35)*s,170*s,24*s,'마법사 50/50',15*s,'#B7E9D8',true);
   let left=isWide?370:200, usable=w-left-20;let each=usable/3;
   for(let i=0;i<3;i++){
     let mw=Math.min(isWide?145:100,each-15);let xx=left+i*each+(each-mw)/2;let yy=Math.max(35,H-205)+(cfg.mode==='back'?(2-i)*14:0);
     if(cfg.mode==='tactical')yy=45+i*12;
     instance(scene,m[i],'적 '+(i+1),xx*s,yy*s,mw*s);
     const labelY=Math.min(H-58,yy+mw*1.17);
     txt(scene,'적 의도',(xx-2)*s,(yy-25)*s,(mw+30)*s,23*s,'공격 4',15*s,'#F0AC97',true);
     txt(scene,'적 체력',(xx-5)*s,labelY*s,(mw+45)*s,24*s,'잉크 '+String.fromCharCode(65+i)+' 12/12',14*s);
     rect(scene,'체력 트랙',xx*s,(labelY+25)*s,mw*s,6*s,'#52605B',3*s);rect(scene,'체력',xx*s,(labelY+25)*s,mw*s,6*s,'#E48978',3*s);
   }
 }
}
function makeBoard(screen,cfg,s){
 const [x,y,w,H]=cfg.board;const p=frame(screen,'주문 도안',x*s,y*s,w*s,H*s,'#273E46',10*s);
 txt(p,'제작판 제목',20*s,10*s,(w-40)*s,28*s,'주문서  ·  불씨 완성 / 방벽 미완성',16*s,'#DCECE4',true);
 const c=Math.min(64,(w-30)/6,(H-52)/3),gx=(w-c*6)/2,gy=43;
 for(let row=0;row<3;row++)for(let col=0;col<6;col++){
   const cx=gx+col*c,cy=gy+row*c;rect(p,'격자 '+(row+1)+'-'+(col+1),cx*s,cy*s,(c-2)*s,(c-2)*s,'#142830',0,'#40565C',1*s);
   vector(p,'대각선',cx*s,cy*s,`M 0 0 L ${(c-2)*s} ${(c-2)*s}`,null,'#475E63',0.7*s);
   vector(p,'대각선',cx*s,cy*s,`M ${(c-2)*s} 0 L 0 ${(c-2)*s}`,null,'#475E63',0.7*s);
 }
 rect(p,'불씨 정사각형',(gx+2)*s,(gy+c+2)*s,(c-6)*s,(c-6)*s,'#A9C8BE',0,'#F1B85E',3*s);
 vector(p,'불씨 1/4 삼각형',gx*s,gy*s,`M 0 ${c*s} L ${c*s/2} ${c*s/2} L ${c*s} ${c*s} Z`,'#A9C8BE','#F1B85E',3*s);
 rect(p,'방벽 정사각형',(gx+c+2)*s,(gy+2)*s,(c-6)*s,(c-6)*s,'#304B53',0,'#70C8E3',3*s);
 vector(p,'방벽 1/4 삼각형',(gx+c)*s,(gy+c)*s,`M 0 0 L ${c*s} 0 L ${c*s/2} ${c*s/2} Z`,'#304B53','#70C8E3',3*s);
}
function makeSpells(screen,cfg,s){
 const [x,y,w,H]=cfg.spells;const p=frame(screen,'주문 시전',x*s,y*s,w*s,H*s,'#263D45',10*s);
 txt(p,'주문 제목',18*s,10*s,(w-36)*s,28*s,'완성 주문',16*s,'#B7CAC6',true);
 const rowH=Math.min(64,(H-52)/2);for(let i=0;i<2;i++){
  let yy=42+i*(rowH+8);rect(p,'주문 행',14*s,yy*s,(w-28)*s,rowH*s,'#3B5359',6*s);rect(p,'주문 색',14*s,yy*s,5*s,rowH*s,i===0?'#F1B85E':'#70C8E3',2*s);
  txt(p,'주문 효과',28*s,(yy+10)*s,(w-155)*s,28*s,i===0?'불씨  ·  단일 피해 6':'방벽  ·  방어 7',16*s,'#EDF4EF',true);
  rect(p,'시전 버튼',(w-118)*s,(yy+8)*s,90*s,(rowH-16)*s,i===0?'#C18C43':'#526266',5*s);
  txt(p,'버튼 레이블',(w-108)*s,(yy+13)*s,75*s,25*s,i===0?'시전':'미완성',14*s);
 }
}
function makeHand(screen,cfg,s){
 const [x,y,w,H]=cfg.hand;const p=frame(screen,'손패',x*s,y*s,w*s,H*s,'#243A42',10*s);
 txt(p,'손패 제목',16*s,7*s,(w-32)*s,27*s,'손패  ·  조각을 도안으로 드래그',16*s,'#C7DAD2',true);
 const narrow=w<500;const cols=narrow?2:5;const gap=8;const right=narrow?0:120;const cw=(w-32-right-gap*(cols-1))/cols;const ch=narrow?Math.min(105,(H-100)/3):Math.min(90,H-44);
 const labels=['정사각형','1/4 삼각형','반칸 삼각형','직사각형','1/4 삼각형'];
 for(let i=0;i<5;i++){
  let xx=16+(i%cols)*(cw+gap),yy=40+Math.floor(i/cols)*(ch+8);const card=frame(p,'조각 '+labels[i],xx*s,yy*s,cw*s,ch*s,'#4C6268',5*s);
  let size=Math.min(38,ch-28,cw-15);let px=(cw-size)/2,py=5;
  if(i===0)rect(card,'정사각형',px*s,py*s,size*s,size*s,'#DFEBE3');
  if(i===1||i===4)vector(card,'1/4 삼각형',px*s,py*s,`M 0 ${size*s} L ${size*s/2} ${size*s/2} L ${size*s} ${size*s} Z`,'#DFEBE3');
  if(i===2)vector(card,'반칸 삼각형',px*s,py*s,`M 0 0 L ${size*s} 0 L ${size*s} ${size*s} Z`,'#DFEBE3');
  if(i===3)rect(card,'직사각형',(px-size*.35)*s,(py+size*.2)*s,size*1.7*s,size*.6*s,'#DFEBE3');
  txt(card,'조각 이름',3*s,(ch-24)*s,(cw-6)*s,20*s,labels[i],12*s,'#F2F5EF');
 }
 const tx=narrow?16:w-110,ty=narrow?H-45:Math.max(45,(H-42)/2);
 rect(p,'턴 종료 버튼',tx*s,ty*s,(narrow?w-32:94)*s,33*s,'#A87C43',5*s);txt(p,'턴 종료', (tx+12)*s,(ty+5)*s,100*s,23*s,'턴 종료',14*s,'#FFFFFF',true);
}
const result=[];
for(let index=start;index<end;index++){
 const cfg=variants[index], col=index%3,row=Math.floor(index/3),s=1.35;
 const tile=frame(target,'전투 UI '+String(index+1).padStart(2,'0')+' · '+cfg.name,(150+col*2380),(100+row*1450),2160,1320,null);
 txt(tile,'시안 이름',0,4,2100,45,(index+1)+' / 10   '+cfg.name,32,'#E9F2EE',true);
 txt(tile,'동선 설명',0,49,2100,28,cfg.note,20,'#AFC5BD');
 const screen=frame(tile,'전투 화면',0,90,2160,1215,'#111F28',14);screen.clipsContent=true;
 txt(screen,'플레이어 정보',26*s,18*s,450*s,32*s,'체력 50/50   ·   방어 0',19*s,'#B7EAD9',true);
 txt(screen,'전투 정보',675*s,18*s,350*s,32*s,'전투 1 / 6',19*s,'#E7F0E9',true);
 txt(screen,'턴 정보',1200*s,18*s,380*s,32*s,'턴 1   ·   남은 배치 3',19*s,'#E7F0E9',true);
 makeScene(screen,cfg,s);makeBoard(screen,cfg,s);makeSpells(screen,cfg,s);makeHand(screen,cfg,s);
 result.push({id:tile.id,name:tile.name,screenId:screen.id,x:tile.x,y:tile.y,width:tile.width,height:tile.height});
}
return {createdNodeIds:created,mutatedNodeIds:[target.id],screens:result,count:result.length,assetBankId:bank.id};
