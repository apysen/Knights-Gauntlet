// Split the approved sheets without redrawing, keying black, or mirroring lighting.
// First run: node Tools/Art/import-supplied-chess.cjs <attachment-directory>
// Later runs rebuild from the preserved ArtSource/SuppliedChess originals.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'ArtSource/SuppliedChess');
const output = path.join(root, 'Assets/Resources/Comic');
const sheets = {
  king: 'cb87919d-934b-43a7-92a3-474bce16c6d3',
  queen: '01fdd118-b4b4-4c29-b8ce-f6fa8d6c795d',
  bishop: 'c7262534-1936-4bf3-b443-b1f35600e0f5',
  rook: 'cdaf289c-71e7-4a7d-8e2b-00535859a204',
  pawn: '0142e279-f02d-4007-96ee-235910b47784',
  knight: '0b7e3b24-68c2-40ae-bbc6-1a1e3f44233f'
};
const heights = { king: 456, queen: 448, bishop: 432, rook: 416, pawn: 380, knight: 440 };
async function main() {
  fs.mkdirSync(source, { recursive: true });
  fs.mkdirSync(output, { recursive: true });
  const report = [], previews = [];
  for (const [piece, attachment] of Object.entries(sheets)) {
    const original = path.join(source, piece + '-sheet.png');
    if (!fs.existsSync(original)) {
      if (!process.argv[2]) throw new Error('Missing preserved sheet: ' + original);
      fs.copyFileSync(path.join(process.argv[2], 'codex-clipboard-' + attachment + '.png'), original);
    }
    const { data, info } = await sharp(original).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const halves = [];
    for (let side = 0; side < 2; side++) {
      const start = Math.floor(side * info.width / 2), end = Math.floor((side + 1) * info.width / 2);
      let left = end, right = start, top = info.height, bottom = 0;
      // Ignore tiny alpha specks for bounds, retaining a small margin around the silhouette.
      for (let y = 0; y < info.height; y++) for (let x = start; x < end; x++) {
        if (data[(y * info.width + x) * 4 + 3] < 24) continue;
        left = Math.min(left, x); right = Math.max(right, x);
        top = Math.min(top, y); bottom = Math.max(bottom, y);
      }
      if (left > right) throw new Error('Empty half: ' + piece);
      left = Math.max(start, left - 2); right = Math.min(end - 1, right + 2);
      top = Math.max(0, top - 2); bottom = Math.min(info.height - 1, bottom + 2);
      halves.push({ left, top, width: right - left + 1, height: bottom - top + 1 });
    }
    // A shared scale per pair avoids a size pop when turning. Every base rests at y=476.
    const scale = Math.min(heights[piece] / Math.max(...halves.map(h => h.height)),
      440 / Math.max(...halves.map(h => h.width)));
    for (let side = 0; side < 2; side++) {
      const facing = side ? 'back' : 'front', bounds = halves[side];
      const width = Math.round(bounds.width * scale), height = Math.round(bounds.height * scale);
      const crop = await sharp(original).extract(bounds).resize(width, height).png().toBuffer();
      const name = `supplied-${piece}-${facing}.png`, file = path.join(output, name);
      await sharp({ create: { width: 512, height: 512, channels: 4, background: '#00000000' } })
        .composite([{ input: crop, left: Math.floor((512 - width) / 2), top: 476 - height }]).png().toFile(file);
      const meta = fs.readFileSync(path.join(output, 'graphic-knight-front-idle.png.meta'), 'utf8')
        .replace(/guid: [a-f0-9]+/, 'guid: ' + crypto.createHash('md5').update('knights-gauntlet/' + name).digest('hex'));
      if (!fs.existsSync(file + '.meta')) fs.writeFileSync(file + '.meta', meta);
      report.push({ piece, facing, bounds, outputWidth: width, outputHeight: height, baseY: 476 });
      previews.push({ input: await sharp(file).resize(160, 160).toBuffer(), left: Object.keys(sheets).indexOf(piece) * 160, top: side * 190 });
    }
  }
  const labels = `<svg width="960" height="380">${Object.keys(sheets).map((p, i) =>
    `<text x="${i * 160 + 80}" y="180" text-anchor="middle" fill="#151512" font-size="16" font-family="sans-serif">${p.toUpperCase()}</text>`).join('')}</svg>`;
  await sharp({ create: { width: 960, height: 380, channels: 4, background: '#F3E9D8' } })
    .composite([...previews, { input: Buffer.from(labels), left: 0, top: 0 }]).png().toFile(path.join(source, 'lineup.png'));
  fs.writeFileSync(path.join(source, 'import-layout.json'), JSON.stringify(report, null, 2) + '\n');
  console.log('Preserved 6 originals; imported 12 transparent front/back sprites and a lineup.');
}
main().catch(e => { console.error(e); process.exitCode = 1; });
