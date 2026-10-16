// Graphic chess art: editable vector source, flat color, spot-black shadows.
// Run with Node and sharp available through node_modules or NODE_PATH.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),src=path.join(root,'ArtSource/GraphicChess'),out=path.join(root,'Assets/Resources/Comic');
fs.mkdirSync(src,{recursive:true});fs.mkdirSync(out,{recursive:true});
const ink='#111210',color='#bababa',paper='#fff6e7';
const p=(d,c=ink)=>`<path d="${d}" fill="${c}"/>`;
const ellipse=(cx,cy,rx,ry,c=ink)=>`<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="${c}"/>`;
const base=p('M87 181 C79 194 59 203 57 213 C51 221 56 231 75 235 C104 243 158 243 184 234 C201 228 204 218 198 211 C196 202 178 192 171 181 Z')
  +p('M87 187 C80 199 66 204 67 212 C84 222 123 225 157 220 C132 214 115 210 119 200 L129 187 Z',color)
  +p('M66 221 C81 230 118 234 151 230 C115 240 76 234 66 229 Z',color)
  +p('M78 207 L99 196 L114 207 L96 214 Z',paper)
  +p('M65 224 C75 224 82 228 83 231 C72 231 66 229 65 224 Z',paper);
const stem=p('M105 101 C108 129 105 159 89 186 C103 198 156 198 171 185 C153 158 151 130 154 101 Z')
  +p('M105 105 C111 132 109 165 97 182 C105 190 124 192 139 188 C125 167 124 135 130 105 Z',color)
  +p('M108 139 L103 176 L111 180 L115 149 Z',paper);
const collar=(y=107)=>ellipse(128,y,43,13)+p(`M88 ${y-5} C105 ${y+2} 132 ${y+6} 153 ${y+2} C139 ${y+12} 104 ${y+12} 86 ${y+3} Z`,color)
  +p(`M94 ${y-4} L114 ${y-1} L105 ${y+2} L90 ${y-1} Z`,paper);
const knight=back=>{
  const shape=back
    ?p('M93 184 C92 158 96 131 94 112 C77 98 78 77 91 55 L88 27 L109 43 L127 31 L147 44 L168 28 L163 59 C178 87 178 104 160 121 C158 146 164 165 172 184 Z')
      +p('M96 47 L99 73 L90 89 L102 114 L104 144 L99 180 L127 188 L129 150 L119 126 L128 109 L116 91 L124 75 L112 55 Z',color)
      +p('M101 62 L106 85 L99 94 L109 113 L107 130 L102 112 L92 91 Z',paper)
      +p('M132 47 L147 60 L140 79 L150 99 L141 116 L151 137 L144 165 L151 179 L133 174 L138 148 L127 131 L134 111 L123 95 L132 74 Z',color)
    :p('M80 184 C85 162 104 141 101 117 L70 130 L53 115 L60 100 L86 72 L94 54 L93 26 L113 45 L141 31 C166 34 187 52 192 81 L176 95 L173 119 C158 135 155 159 172 184 Z')
      +p('M102 46 L103 72 L68 109 L75 118 L113 102 L125 114 C125 140 107 160 101 181 L132 189 C136 160 141 132 156 113 L155 86 L171 77 C165 54 141 42 121 44 Z',color)
      +p('M103 64 L109 51 L112 72 L78 106 L68 111 Z',paper)
      +p('M112 132 C108 148 101 162 98 175 L105 178 L121 137 Z',paper)
      +p('M123 73 L143 69 L135 78 Z')
      +p('M74 109 L87 105 L82 114 Z');
  return {body:shape,base};
};
const rook=back=>({body:stem+p('M87 103 L74 81 L69 36 L91 32 L95 55 L113 53 L113 30 H137 L139 54 L158 57 L165 33 L187 39 L179 83 L166 104 Z')
  +p('M80 42 L90 67 L116 70 L113 39 L128 39 L128 91 C109 93 96 91 89 85 Z',color)
  +p('M94 79 L113 81 L105 91 L92 89 Z',paper)
  +p('M152 65 L158 43 L168 43 L163 69 Z',paper)
  +(back?'':p('M83 55 L96 60 L98 71 L88 69 Z'))+collar(105),base});
const bishop=back=>({body:stem+p('M126 25 C145 40 164 56 165 75 C166 88 151 102 132 105 C111 104 90 93 91 77 C92 57 113 44 126 25 Z')
  +p('M124 35 C107 53 99 69 102 82 C105 91 116 95 131 95 C120 78 125 65 136 51 Z',color)
  +p(back?'M126 31 L147 51 L131 69 L124 64 L136 50 Z':'M128 30 L145 46 L122 76 L113 71 L135 46 Z')
  +p('M112 58 L117 49 L110 76 L106 83 Z',paper)+collar(106),base});
const queen=back=>({body:stem+p('M92 102 L76 58 L91 62 L97 76 L109 72 L112 45 L124 42 L139 72 L154 72 L169 53 L181 55 L166 103 Z')
  +p('M85 65 L94 92 L126 95 L125 76 L118 50 L117 79 L100 85 Z',color)
  +p('M93 68 L99 73 L99 83 L93 81 Z',paper)
  +p('M136 78 L152 79 L145 87 L136 88 Z',paper)
  +ellipse(116,43,9,9,color)+ellipse(175,52,7,7)+ellipse(81,56,7,7,color)
  +collar(105),base});
const pawn=back=>({body:stem+ellipse(128,72,34,36)
  +p('M123 37 C102 43 94 60 97 78 C100 91 113 100 130 101 C118 87 116 70 126 56 C133 48 132 41 123 37 Z',color)
  +p('M108 51 L119 45 L111 64 L102 68 Z',paper)+collar(108),base});
const designs={knight,rook,bishop,queen,pawn};
const palette={knight:'#EFB000',rook:'#CF3524',bishop:'#EFB000',queen:'#CF3524',pawn:'#EDE2CE'};
const wrap=s=>`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">${s}</svg>`;
const assets={};
async function write(name,svg){
  fs.writeFileSync(path.join(src,name+'.svg'),svg);assets[name]=svg;
  const file=path.join(out,name+'.png');await sharp(Buffer.from(svg)).resize(512,512).png().toFile(file);
  if(!fs.existsSync(file+'.meta'))fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${crypto.createHash('md5').update('knights-gauntlet/'+name).digest('hex')}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  textureType: 8\n  spriteMode: 1\n  spritePixelsToUnits: 512\n  spriteMeshType: 0\n  alphaIsTransparency: 1\n  spritePivot: {x: 0.5, y: 0.5}\n  maxTextureSize: 512\n  textureCompression: 0\n`);
}
async function main(){
  const previews=[];let i=0;
  for(const [name,draw]of Object.entries(designs)){
    for(const facing of ['front','back'])for(const pose of ['idle','attack','hit']){
      const art=draw(facing==='back');let top=art.body;
      if(pose==='attack')top=`<g transform="translate(-10 15) skewX(5) scale(1.04 .92)">${top}</g>`;
      if(pose==='hit')top=`<g transform="translate(10 -4) skewX(-4) scale(.97 1.02)">${top}</g>`;
      await write(`graphic-${name}-${facing}-${pose}`,wrap(top+art.base));
      if(pose==='idle'){
        const colored=(top+art.base).replaceAll(color,palette[name]);
        previews.push(`<g transform="translate(${i*240} ${facing==='front'?0:300})"><svg width="240" height="256" viewBox="0 0 256 256">${colored}</svg><text x="120" y="280" fill="${ink}" font-family="sans-serif" font-size="15" text-anchor="middle">${name.toUpperCase()} · ${facing.toUpperCase()}</text></g>`);
      }
    }i++;
  }
  const sheet=`<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="600"><rect width="1200" height="600" fill="#F3E9D8"/>${previews.join('')}</svg>`;
  await sharp(Buffer.from(sheet)).png().toFile(path.join(src,'graphic-lineup.png'));
  const tileW=144,tileH=112,top=120;
  const grid=[];
  for(let r=0;r<10;r++)for(let c=0;c<5;c++)grid.push(`<rect x="${c*tileW}" y="${top+r*tileH}" width="${tileW}" height="${tileH}" fill="${(r+c)%2?'#DED2BB':'#F3E9D8'}"/>`);
  const pieces=[['rook',0,1],['queen',2,1],['bishop',4,1],['knight',1,3],['pawn',3,4],['knight',2,7,'back']];
  for(const[name,c,r,facing='front']of pieces){
    const tint=name==='knight'&&facing==='front'?'#CF3524':palette[name];
    const svg=assets[`graphic-${name}-${facing}-idle`].replaceAll(color,tint);
    grid.push(`<g transform="translate(${c*tileW+16} ${top+r*tileH})">${svg.replace('<svg ','<svg width="112" height="112" ')}</g>`);
  }
  for(const[c,r]of[[0,5],[4,6],[1,8]])grid.push(`<g transform="translate(${c*tileW+53} ${top+r*tileH+37})"><path d="M0 0 L36 36 M36 0 L0 36" stroke="#CF3524" stroke-width="10"/></g>`);
  for(const[c,r]of[[0,6],[1,5],[3,5],[0,8],[4,8],[1,9],[3,9]])grid.push(`<circle cx="${c*tileW+72}" cy="${top+r*tileH+56}" r="6" fill="#92701B"/>`);
  const board=`<svg xmlns="http://www.w3.org/2000/svg" width="720" height="1280"><rect width="720" height="1280" fill="#151512"/><text x="30" y="72" fill="#EFB000" font-size="48" font-family="sans-serif" font-weight="bold">150</text><text x="360" y="66" fill="#F3E9D8" font-size="23" font-family="sans-serif" text-anchor="middle">KNIGHT'S GAUNTLET</text>${grid.join('')}</svg>`;
  await sharp(Buffer.from(board)).png().toFile(path.join(src,'board-layout-preview.png'));
  console.log('Built 30 graphic chess sprites, lineup, and source-layout board preview.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
