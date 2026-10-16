// Shared hazard, pickup and impact art. Node + sharp; no image service required.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),src=path.join(root,'ArtSource/GraphicChess'),out=path.join(root,'Assets/Resources/Comic');
const ink='#111210',color='#bababa',paper='#fff6e7';
const p=(d,c=color,w=7)=>`<path d="${d}" fill="${c}" stroke="${ink}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round"/>`;
const e=(x,y,rx,ry,c=color)=>`<ellipse cx="${x}" cy="${y}" rx="${rx}" ry="${ry}" fill="${c}" stroke="${ink}" stroke-width="7"/>`;
const shapes={
  'comic-trap-idle':p('M128 28 L149 72 L192 48 L182 94 L226 109 L190 137 L216 177 L171 177 L164 223 L129 191 L94 224 L86 180 L41 177 L64 139 L28 108 L74 96 L66 49 L109 72 Z',ink)
    +e(127,128,64,65)+p('M103 92 L128 117 L153 92 L168 107 L143 132 L168 157 L153 172 L128 147 L103 172 L88 157 L113 132 L88 107 Z',paper),
  'comic-impact':p('M126 24 L146 80 L199 43 L178 99 L237 109 L187 137 L222 187 L164 165 L144 229 L121 178 L67 210 L87 157 L25 143 L80 117 L47 66 L105 87 Z',paper)
    +p('M127 72 L140 109 L177 93 L157 128 L185 149 L146 147 L132 187 L116 150 L80 163 L101 134 L80 109 L115 114 Z'),
  'comic-slash':p('M24 212 Q108 77 230 40 Q143 111 105 187 L133 132 Q98 180 76 222 L86 176 Z',paper,5),
  'comic-fist':p('M24 115 L49 103 L72 105 L76 83 Q79 65 94 65 Q109 65 110 83 L112 101 L116 75 Q119 57 134 58 Q150 59 149 78 L148 100 L155 78 Q162 61 177 68 Q191 75 183 94 L178 106 L188 94 Q200 82 211 94 Q220 104 210 119 L190 149 Q177 168 151 173 L79 168 L51 183 L25 161 Z','#F0B21A')
    +p('M78 112 L112 99 L147 100 L178 111 L195 124 L179 145 Q170 155 155 154 L136 145 L120 155 L97 146 L82 135 Z','#E94B2F',5)
    +p('M27 128 L52 139 L75 127 L81 151 L52 172 L27 156 Z','#111210',5)
    +p('M88 82 Q92 75 98 80 L98 100 L91 109 Z M126 75 Q132 68 138 77 L136 98 L129 105 Z M165 80 Q171 74 177 81 L169 103 L162 108 Z',paper,3)
    +`<path d="M88 124 L108 115 M121 121 L143 115 M154 122 L173 119" fill="none" stroke="${ink}" stroke-width="6" stroke-linecap="round"/>`
    +p('M32 138 L48 145 L44 154 L30 148 Z',paper,3)
    +`<path d="M93 145 Q111 157 126 150" fill="none" stroke="#B82920" stroke-width="5" stroke-linecap="round"/>`,
  'comic-coin':e(135,135,74,80,ink)+e(118,119,74,80)+p('M75 70 L95 57 L80 92 L65 103 Z',paper,0)
    +p('M130 78 L102 117 H126 L107 158 L145 111 H121 Z',paper,5)
};
async function main(){
  fs.mkdirSync(src,{recursive:true});fs.mkdirSync(out,{recursive:true});
  for(const[name,body]of Object.entries(shapes)){
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">${body}</svg>`;
    fs.writeFileSync(path.join(src,name+'.svg'),svg);
    const file=name==='comic-fist'?path.join(root,'Assets/Resources/CombatVfx/comic-fist.png'):path.join(out,name+'.png');
    await sharp(Buffer.from(svg)).resize(256,256).png().toFile(file);
    if(!fs.existsSync(file+'.meta'))fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${crypto.createHash('md5').update('knights-gauntlet/'+name).digest('hex')}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  textureType: 8\n  spriteMode: 1\n  spritePixelsToUnits: 256\n  spriteMeshType: 0\n  alphaIsTransparency: 1\n  maxTextureSize: 256\n`);
  }
  console.log('Built four shared hazard, coin and impact sprites.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
