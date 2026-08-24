// Ported from C# GroupHorizontalTests (sehenswerte src/sehens/paint/GroupHorizontal.cs).
// All 19 C# test methods are here under their original names, plus a few extra
// branch cases (marked "TS extra") the C# suite did not spell out.

import {
  classify,
  GroupMember,
  HorizontalKind,
  subWindow,
  valueWindow,
} from './groupHorizontal';

function m(
  kind: HorizontalKind,
  unit: string,
  left: number,
  right: number,
  log = false
): GroupMember {
  return { kind, unit, left, right, log };
}

describe('classify', () => {
  test('AllNoneStretches', () => {
    const d = classify([m('none', '', 0, 100), m('none', '', 0, 100)]);
    expect(d.mode).toBe('stretch');
  });

  test('NoneDifferingCountsIncompatible', () => {
    const d = classify([m('none', '', 0, 100), m('none', '', 0, 50)]);
    expect(d.mode).toBe('incompatible');
  });

  test('AllAffineSameUnitValueAlignsOverUnion', () => {
    const d = classify([
      m('affine', 'rpm', 0, 6000),
      m('affine', 'rpm', 1000, 4000),
    ]);
    expect(d.mode).toBe('valueAlign');
    expect(d.left).toBeCloseTo(0.0, 9);
    expect(d.right).toBeCloseTo(6000.0, 9);
    expect(d.unit).toBe('rpm');
  });

  test('TimeAndAffineSecondsAreCompatible', () => {
    const d = classify([
      m('time', '', 0, 10), // time contributes unit "s"
      m('affine', 's', 0, 5),
    ]);
    expect(d.mode).toBe('valueAlign');
    expect(d.unit).toBe('s');
    expect(d.right).toBeCloseTo(10.0, 9);
  });

  test('DifferentUnitsIncompatible', () => {
    const d = classify([m('affine', 'rpm', 0, 100), m('affine', 'kph', 0, 50)]);
    expect(d.mode).toBe('incompatible');
  });

  test('NoneMixedWithDomainIncompatible', () => {
    const d = classify([m('none', '', 0, 100), m('affine', 'rpm', 0, 100)]);
    expect(d.mode).toBe('incompatible');
  });

  test('LinAndLogMembersIncompatible', () => {
    // same unit, same range - but one view is log-X: no shared value->pixel map exists,
    // so this must warn instead of silently drawing a linear gutter under a log curve
    const d = classify([
      m('affine', 'u', 0, 100, false),
      m('affine', 'u', 0, 100, true),
    ]);
    expect(d.mode).toBe('incompatible');
  });

  test('AllLogIdenticalRangesValueAlign', () => {
    // identical ranges -> full-pane sub-windows -> each member's own log projection is the
    // same map, so alignment is safe
    const d = classify([
      m('affine', 'Hz', 0, 12000, true),
      m('affine', 'Hz', 0, 12000, true),
    ]);
    expect(d.mode).toBe('valueAlign');
    expect(d.left).toBeCloseTo(0.0, 9);
    expect(d.right).toBeCloseTo(12000.0, 9);
  });

  test('AllLogRaggedRangesIncompatible', () => {
    // differing ranges would need a log-composed sub-window; subWindow is linear -> warn
    const d = classify([
      m('affine', 'Hz', 0, 12000, true),
      m('affine', 'Hz', 0, 6000, true),
    ]);
    expect(d.mode).toBe('incompatible');
  });

  test('NoneMixedLogIncompatible', () => {
    // two plain sample-number traces, one switched to log-X: the shared gutter cannot be
    // right for both -> warn (field report: previously classified Stretch, no warning)
    const d = classify([
      m('none', '', 0, 100, false),
      m('none', '', 0, 100, true),
    ]);
    expect(d.mode).toBe('incompatible');
  });

  test('AllNoneAllLogStillStretches', () => {
    const d = classify([
      m('none', '', 0, 100, true),
      m('none', '', 0, 100, true),
    ]);
    expect(d.mode).toBe('stretch');
  });

  test('FftMixedWithNonFftIncompatible', () => {
    // field report: FFT grouped with a plain trace showed no warning when the FFT trace
    // led the group (the paint layer used to skip classification for FFT leaders)
    const d = classify([m('fft', '', 0, 12000), m('none', '', 0, 100)]);
    expect(d.mode).toBe('incompatible');
  });

  test('AllYtGroupLeftToItsOwnTimeWindow', () => {
    const d = classify([m('yt', '', 0, 1000), m('yt', '', 0, 600)]);
    expect(d.mode).toBe('stretch');
  });

  test('YtMixedWithAnythingIncompatible', () => {
    const ytPlusTime = classify([m('yt', '', 0, 1000), m('time', '', 0, 10)]);
    expect(ytPlusTime.mode).toBe('incompatible');
    const ytPlusFft = classify([m('yt', '', 0, 1000), m('fft', '', 0, 4000)]);
    expect(ytPlusFft.mode).toBe('incompatible');
    const ytPlusNone = classify([m('yt', '', 0, 1000), m('none', '', 0, 500)]);
    expect(ytPlusNone.mode).toBe('incompatible');
  });

  test('AllFftGroupLeftToItsOwnHzPath', () => {
    const d = classify([m('fft', '', 0, 12000), m('fft', '', 0, 12000)]);
    expect(d.mode).toBe('stretch'); // no warning, no overrides
  });

  test('EmptyGroupStretches (TS extra)', () => {
    const d = classify([]);
    expect(d.mode).toBe('stretch');
  });

  test('SingleValueMemberValueAlignsToItself (TS extra)', () => {
    const d = classify([m('time', '', 2, 12)]);
    expect(d.mode).toBe('valueAlign');
    expect(d.left).toBeCloseTo(2.0, 9);
    expect(d.right).toBeCloseTo(12.0, 9);
    expect(d.unit).toBe('s');
  });

  test('TimeWithNonSecondsAffineIncompatible (TS extra)', () => {
    // time always contributes "s"; an affine member in another unit cannot share it
    const d = classify([m('time', '', 0, 10), m('affine', 'rpm', 0, 6000)]);
    expect(d.mode).toBe('incompatible');
  });

  test('OmittedLogFlagTreatedAsLinear (TS extra)', () => {
    // GroupMember.log is optional in the TS shape; undefined must mean lin-X,
    // not "different from an explicit false"
    const a: GroupMember = { kind: 'affine', unit: 'u', left: 0, right: 100 };
    const b = m('affine', 'u', 0, 100, false);
    expect(classify([a, b]).mode).toBe('valueAlign');
    const noneA: GroupMember = { kind: 'none', unit: '', left: 0, right: 100 };
    expect(classify([noneA, m('none', '', 0, 100, false)]).mode).toBe(
      'stretch'
    );
  });
});

describe('subWindow', () => {
  test('SubWindowFullWhenMemberEqualsDomain', () => {
    const { left, width } = subWindow(0, 100, 0, 100, 10, 200);
    expect(left).toBeCloseTo(10.0, 9);
    expect(width).toBeCloseTo(200.0, 9);
  });

  test('SubWindowSubRangeAlignsByValue', () => {
    // A member covering the left half of the domain occupies the left half of the pane
    const { left, width } = subWindow(0, 50, 0, 100, 0, 200);
    expect(left).toBeCloseTo(0.0, 9);
    expect(width).toBeCloseTo(100.0, 9);
    // A member offset into the middle is placed ragged
    const { left: left2, width: width2 } = subWindow(25, 75, 0, 100, 0, 200);
    expect(left2).toBeCloseTo(50.0, 9);
    expect(width2).toBeCloseTo(100.0, 9);
  });

  test('SubWindowZeroSpanFallsBackToPane', () => {
    const { left, width } = subWindow(5, 5, 5, 5, 3, 120);
    expect(left).toBeCloseTo(3.0, 9);
    expect(width).toBeCloseTo(120.0, 9);
  });

  test('SubWindowNegativeSpanFallsBackToPane (TS extra)', () => {
    const { left, width } = subWindow(0, 10, 100, 0, 7, 90);
    expect(left).toBeCloseTo(7.0, 9);
    expect(width).toBeCloseTo(90.0, 9);
  });
});

describe('window', () => {
  test('WindowAppliesZoomPanToFullDomain', () => {
    const members = [m('affine', 'rpm', 0, 200), m('affine', 'rpm', 0, 100)];
    // full view
    const full = valueWindow(members, 1.0, 0.0);
    expect(full.mode).toBe('valueAlign');
    expect(full.left).toBeCloseTo(0.0, 9);
    expect(full.right).toBeCloseTo(200.0, 9);
    // zoomed to the middle half: window [50,150]
    const zoomed = valueWindow(members, 0.5, 0.25);
    expect(zoomed.left).toBeCloseTo(50.0, 9);
    expect(zoomed.right).toBeCloseTo(150.0, 9);
  });

  test('WindowClampsPanInsideTheDomain', () => {
    const members = [m('affine', 'u', 0, 200)];
    const d = valueWindow(members, 0.5, 0.9);
    expect(d.left).toBeCloseTo(100.0, 9); // pan clamped to 0.5
    expect(d.right).toBeCloseTo(200.0, 9); // window ends at the domain edge
  });

  test('WindowNonValueAlignUnchanged', () => {
    const members = [m('none', '', 0, 100), m('none', '', 0, 100)];
    const d = valueWindow(members, 0.5, 0.1);
    expect(d.mode).toBe('stretch');
  });

  test('WindowClampsZoomToUnity (TS extra)', () => {
    const members = [m('affine', 'u', 10, 110)];
    const d = valueWindow(members, 2.0, -0.5);
    expect(d.left).toBeCloseTo(10.0, 9);
    expect(d.right).toBeCloseTo(110.0, 9);
  });
});
