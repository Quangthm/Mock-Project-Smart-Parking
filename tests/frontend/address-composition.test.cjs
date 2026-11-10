const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

// Same minimal VM harness as the other tests in this folder: transpile the pure
// TypeScript module to CommonJS and run it in an isolated context whose `require`
// throws. This only works because addressComposition.ts has zero runtime imports.
function load(file) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, require: name => { throw new Error(`Unexpected import: ${name}`); } };
  vm.runInNewContext(js, context);
  return context.exports;
}

const address = load('roles/owner/parking-lots/addressComposition.ts');

// --- composeAddress -------------------------------------------------------

test('composeAddress emits street, district, city in that canonical order', () => {
  assert.equal(address.composeAddress('72 Le Thanh Ton', 'Quận 1', 'HCM'), '72 Le Thanh Ton, Quận 1, TP. Hồ Chí Minh');
  assert.equal(address.composeAddress('12 X', 'Quận Cầu Giấy', 'HN'), '12 X, Quận Cầu Giấy, Hà Nội');
  assert.equal(address.composeAddress('  ', '', 'HCM'), 'TP. Hồ Chí Minh');
});

// --- parseSiteAddress -----------------------------------------------------

function assertParsed(actual, expected) {
  // The module runs in a separate vm context, so compare fields rather than
  // object identity/prototype.
  assert.equal(actual.cityCode, expected.cityCode);
  assert.equal(actual.district, expected.district);
  assert.equal(actual.street, expected.street);
}

test('parseSiteAddress splits a stored address back into city/district/street', () => {
  assertParsed(address.parseSiteAddress('72 Le Thanh Ton, Quận 1, TP. Hồ Chí Minh'), {
    cityCode: 'HCM',
    district: 'Quận 1',
    street: '72 Le Thanh Ton',
  });
  assertParsed(address.parseSiteAddress('12 X, Quận Cầu Giấy, Hà Nội'), {
    cityCode: 'HN',
    district: 'Quận Cầu Giấy',
    street: '12 X',
  });
});

test('parseSiteAddress keeps multi-part street lines before the district', () => {
  assertParsed(address.parseSiteAddress('72 Le Thanh Ton Street, Ben Nghe Ward, Quận 1, TP. Hồ Chí Minh'), {
    cityCode: 'HCM',
    district: 'Quận 1',
    street: '72 Le Thanh Ton Street, Ben Nghe Ward',
  });
});

test('parseSiteAddress can infer the city from a known district when the city name is absent', () => {
  assertParsed(address.parseSiteAddress('12 X, Quận Hải Châu'), {
    cityCode: 'DN',
    district: 'Quận Hải Châu',
    street: '12 X',
  });
});

test('parseSiteAddress reports an unmapped address instead of guessing a city', () => {
  assertParsed(address.parseSiteAddress('Somewhere unknown'), {
    cityCode: null,
    district: null,
    street: 'Somewhere unknown',
  });
});

test('parseSiteAddress round-trips the addresses the form composes', () => {
  for (const original of [
    '72 Le Thanh Ton, Quận 1, TP. Hồ Chí Minh',
    '12 X, Quận Cầu Giấy, Hà Nội',
    '72 Le Thanh Ton Street, Ben Nghe Ward, Quận 1, TP. Hồ Chí Minh',
  ]) {
    const parsed = address.parseSiteAddress(original);
    assert.equal(address.composeAddress(parsed.street, parsed.district, parsed.cityCode), original);
  }
});

// --- resolveSiteAddress (the name-only-edit rule) --------------------------

test('resolveSiteAddress leaves the stored address untouched when only the name changes', () => {
  const stored = '12 X, Quận Cầu Giấy, Hà Nội';
  assert.equal(
    address.resolveSiteAddress({
      storedAddress: stored,
      addressTouched: false,
      // The form's fallback state must not leak into the saved address.
      street: 'Somewhere else',
      district: 'Quận 1',
      cityCode: 'HCM',
    }),
    stored,
  );
});

test('resolveSiteAddress applies a real city/district/street edit', () => {
  assert.equal(
    address.resolveSiteAddress({
      storedAddress: '12 X, Quận Cầu Giấy, Hà Nội',
      addressTouched: true,
      street: '99 New Road',
      district: 'Quận 7',
      cityCode: 'HCM',
    }),
    '99 New Road, Quận 7, TP. Hồ Chí Minh',
  );
});

test('resolveSiteAddress composes for new sites without a stored address', () => {
  assert.equal(
    address.resolveSiteAddress({
      storedAddress: '',
      addressTouched: false,
      street: '1 New Site',
      district: 'Quận 3',
      cityCode: 'HCM',
    }),
    '1 New Site, Quận 3, TP. Hồ Chí Minh',
  );
});
