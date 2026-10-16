// Supply stable import identities while the Editor is unavailable.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'../..');
const files=['Assets/Scripts/View/ComicArt.cs','Assets/Scripts/View/GameView.Comic.cs','Assets/Editor/ComicArtPreview.cs','Assets/Resources/Comic/ComicPalette.mat','Assets/Resources/Comic'];
for(const name of files){
  const file=path.join(root,name),meta=file+'.meta';if(fs.existsSync(meta))continue;
  const guid=crypto.createHash('md5').update('knights-gauntlet/'+name).digest('hex');
  let content=`fileFormatVersion: 2\nguid: ${guid}\n`;
  if(fs.statSync(file).isDirectory())content+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n';
  else if(name.endsWith('.cs'))content+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n';
  else content+='NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n';
  fs.writeFileSync(meta,content+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n');
}
const spriteDirectory=path.join(root,'Assets/Resources/Comic');
for(const name of fs.readdirSync(spriteDirectory)){
  if(!name.endsWith('.png.meta'))continue;
  const file=path.join(root,'Assets/Resources/Comic',name),content=fs.readFileSync(file,'utf8');
  if(content.includes('platformSettings:'))continue;
  fs.writeFileSync(file,content+'  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 512\n    resizeAlgorithm: 0\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n');
}
