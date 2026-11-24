const assert = require('node:assert/strict');
const fs = require('node:fs');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');
const source = fs.readFileSync(require('node:path').join(__dirname, '../../FE/src/lib/assistantBookingPreview.ts'), 'utf8');
const exportsUnderTest = {};
vm.runInThisContext(`(function(exports) {${ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText}\n})`)(exportsUnderTest);
const { interpretBookingTurn } = exportsUnderTest;

// The UTC date is still Nov 10 while the Vietnamese date is Nov 11.
const now = new Date('2026-11-10T18:30:00Z');
const turn = (previous, text) => interpretBookingTurn(previous, text, now);
const start = (text = 'đặt cho tôi chỗ ô tô từ 14h đến 16h mai') => turn(null, text);
const complete = () => turn(turn(start(), 'xe 51A-123.45'), 'bãi FPT');

test('extracts vehicle type and tomorrow in Vietnam, then asks for both missing selections', () => {
  const first = start();
  assert.deepEqual(first.preview, {
    vehicleType: 'CAR', vehicleText: null, siteText: null,
    startAt: '2026-11-12T14:00:00+07:00', endAt: '2026-11-12T16:00:00+07:00',
    timeZone: 'Asia/Ho_Chi_Minh', missingFields: ['vehicle', 'site'],
  });
  assert.match(first.reply, /xe nào.*bãi nào/);
  const next = turn(first, 'xe 51A-123.45');
  assert.deepEqual(next.preview.missingFields, ['site']);
  assert.deepEqual(complete().preview.missingFields, []);
});

test('extracts a complete request with user-entered vehicle and site labels', () => {
  const { preview } = start('Đặt chỗ xe ô tô 51A-123.45 từ 14h30 đến 16:15 ngày 2026-11-15 tại bãi FPT');
  assert.deepEqual(preview.missingFields, []);
  assert.equal(preview.vehicleText, '51A-123.45');
  assert.equal(preview.siteText, 'FPT');
  assert.equal(preview.startAt, '2026-11-15T14:30:00+07:00');
  const siteBeforeHours = start('Đặt chỗ ô tô ở FPT từ 14h đến 16h mai').preview;
  assert.equal(siteBeforeHours.siteText, 'FPT');
  assert.equal(siteBeforeHours.startAt, '2026-11-12T14:00:00+07:00');
  assert.equal(preview.endAt, '2026-11-15T16:15:00+07:00');
});

test('separates timing that follows a site name in natural Vietnamese booking requests', () => {
  for (const text of [
    'Đặt chỗ cho ô tô tại Bitexco ngày mai từ 14h đến 16h',
    'Đặt chỗ ô tô ở Bitexco ngày mai từ 14h đến 16h',
  ]) {
    const { preview } = start(text);
    assert.equal(preview.vehicleType, 'CAR');
    assert.equal(preview.siteText, 'Bitexco');
    assert.equal(preview.startAt, '2026-11-12T14:00:00+07:00');
    assert.equal(preview.endAt, '2026-11-12T16:00:00+07:00');
    assert.deepEqual(preview.missingFields, ['vehicle']);
  }
});

test('keeps the site and timing when a booking request omits vehicle type', () => {
  const { preview } = start('Tôi muốn đặt chỗ tại bãi Bitexco ngày mai từ 14h đến 16h');
  assert.equal(preview.vehicleType, null);
  assert.equal(preview.siteText, 'Bitexco');
  assert.equal(preview.startAt, '2026-11-12T14:00:00+07:00');
  assert.equal(preview.endAt, '2026-11-12T16:00:00+07:00');
  assert.deepEqual(preview.missingFields, ['vehicleType', 'vehicle']);
});

test('asks for exact hours and keeps the known day across clarification', () => {
  const first = start('Đặt chỗ ô tô chiều mai');
  assert.deepEqual(first.preview.missingFields, ['timeRange', 'vehicle', 'site']);
  assert.equal(first.fields.day, '2026-11-12');
  assert.equal(start('Đặt chỗ ô tô 2 đến 4 mai').preview.vehicleText, null);
  assert.equal(turn(first, '2h đến 4h').preview.startAt, null);
  assert.equal(turn(first, '2:00 đến 4:00').preview.startAt, null);
  assert.equal(turn(first, '2h đến 16h').preview.startAt, null);
  assert.equal(turn(first, '02:00 đến 16h').preview.startAt, '2026-11-12T02:00:00+07:00');
  assert.equal(turn(first, '10h đến 14h').preview.startAt, '2026-11-12T10:00:00+07:00');
  assert.equal(turn(first, '2h đến 4h chiều').preview.startAt, '2026-11-12T14:00:00+07:00');
  assert.equal(turn(first, '02:00 đến 04:00').preview.startAt, '2026-11-12T02:00:00+07:00');
});

test('asks for a day when only hours were given and retains those hours', () => {
  const first = start('Đặt chỗ ô tô từ 14h đến 16h');
  assert.equal(first.preview.startAt, null);
  assert.match(first.reply, /ngày nào/);
  assert.equal(turn(first, 'mai').preview.startAt, '2026-11-12T14:00:00+07:00');
});

test('edits the vehicle and clears a previously selected vehicle when its type changes', () => {
  const original = complete();
  const next = turn(original, 'đổi xe sang xe 51B-543.21');
  assert.equal(next.preview.vehicleText, '51B-543.21');
  assert.equal(next.preview.vehicleType, 'CAR');
  const changedType = turn(next, 'xe máy thay ô tô');
  assert.equal(changedType.preview.vehicleType, 'MOTORCYCLE');
  assert.equal(changedType.preview.vehicleText, null);
  assert.equal(changedType.preview.siteText, 'FPT');
  assert.equal(changedType.preview.startAt, original.preview.startAt);
  assert.deepEqual(changedType.preview.missingFields, ['vehicle']);
  assert.deepEqual(turn(next, 'đổi sang ô tô hoặc xe máy').preview.missingFields, ['vehicleType', 'vehicle']);
});

test('edits site and asks instead of resolving alternatives or ordinal references', () => {
  const next = turn(complete(), 'đổi bãi sang Bãi Trung Tâm');
  assert.equal(next.preview.siteText, 'Trung Tâm');
  assert.equal(next.preview.vehicleText, '51A-123.45');
  for (const text of ['đổi bãi sang FPT hoặc Vincom', 'chọn bãi thứ hai']) {
    const ambiguous = turn(next, text);
    assert.deepEqual(ambiguous.preview.missingFields, ['site']);
    assert.equal(ambiguous.preview.siteText, null);
  }
});

test('edits hours on the selected date and clears a superseded range on ambiguous edits', () => {
  const next = turn(complete(), 'đổi giờ sang 15h đến 17h');
  assert.equal(next.preview.startAt, '2026-11-12T15:00:00+07:00');
  assert.equal(next.preview.endAt, '2026-11-12T17:00:00+07:00');
  assert.equal(next.preview.siteText, 'FPT');
  for (const text of ['đổi sang 14h', 'đổi giờ sang 18h đến 16h', 'đổi giờ sang 23h đến 02h', 'đổi giờ sang 15h60 đến 17h']) {
    const invalid = turn(next, text);
    assert.deepEqual(invalid.preview.missingFields, ['timeRange']);
    assert.equal(invalid.preview.startAt, null);
    assert.equal(invalid.preview.endAt, null);
  }
  assert.equal(turn(next, 'đổi giờ sang ngày kia').preview.startAt, '2026-11-13T15:00:00+07:00');
});

test('rejects conflicting and invalid dates and past intervals', () => {
  for (const text of ['ngày 2026-11-31', 'ngày 2026-11-10', 'mai hoặc ngày kia', 'thứ sáu',
    'đổi giờ sang 15h đến 17h thứ sáu']) {
    assert.equal(turn(complete(), text).preview.startAt, null);
    assert.equal(turn(turn(complete(), text), '15h đến 17h').preview.startAt, null);
  }
  assert.equal(interpretBookingTurn(null, 'đặt chỗ ô tô hôm nay từ 14h đến 16h',
    new Date('2026-11-11T08:00:00Z')).preview.startAt, null);
});

test('keeps unsupported weekdays out of the vehicle label and asks for a date', () => {
  for (const weekday of ['thứ sáu', 'chủ nhật']) {
    const result = start(`Đặt chỗ ô tô ${weekday} từ 14h đến 16h`);
    assert.equal(result.preview.vehicleText, null);
    assert.equal(result.preview.startAt, null);
    assert.match(result.reply, /ngày nào/);
  }
});

test('handles bare plates and polite plate replies inside booking intake', () => {
  for (const message of ['51A-123.45', 'xe 51A-123.45 nhé?', 'xe 51A.123.45 nhé?',
    'xe 59-X1 123.45', 'đổi xe sang xe 51A-123.45 nhé?']) {
    const result = turn(start(), message);
    assert.match(result.preview.vehicleText, /123\.45$/);
    assert.deepEqual(result.preview.missingFields, ['site']);
  }
  const first = start('Đặt chỗ xe ô tô 51A-123.45 từ 14h đến 16h mai nhé?');
  assert.equal(first.preview.vehicleText, '51A-123.45');
  assert.deepEqual(first.preview.missingFields, ['site']);
});

test('asks for one edit per turn without storing another edit as part of a label', () => {
  const existing = complete();
  for (const message of ['Đổi bãi sang bãi Vincom và đổi giờ sang 15h đến 17h',
    'đổi bãi sang bãi Vincom và giờ sang 15h đến 17h']) {
    const result = turn(existing, message);
    assert.deepEqual(result.preview, existing.preview);
    assert.match(result.reply, /từng thay đổi/);
  }
});

test('confirmation only reviews, a fresh request replaces prior selections, and S06/FAQ still fall through', () => {
  const existing = complete();
  const confirmation = turn(existing, 'đồng ý');
  assert.deepEqual(confirmation.preview, existing.preview);
  assert.match(confirmation.reply, /xem lại thông tin/);
  assert.equal(turn(existing, 'Đặt chỗ ô tô ngày mai').preview.siteText, null);
  for (const message of ['Đổi bãi sang FPT', 'đồng ý', 'xe 51A-123.45']) assert.equal(turn(null, message), null);
  for (const message of ['Cách đặt chỗ?', 'Đặt chỗ trước tối đa bao nhiêu ngày', 'Tìm bãi FPT',
    'Xe của tôi', 'xe cua toi', 'Giờ mở cửa của SmartPark', 'Lịch đặt của tôi',
    'Ngày mai tôi có lịch đặt không', 'ngay mai toi co lich dat khong']) assert.equal(turn(existing, message), null);
});
