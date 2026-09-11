import { describe, expect, it } from 'vitest';
import { PatchUtils } from '../src/utils/patch-utils';

describe('J3 PatchUtils.mergeOperations', () => {
  it('J3: keeps every add to the end of an array', () => {
    const merged = PatchUtils.mergeOperations(
      [{ op: 'add', path: '/-', value: { id: 1 } }],
      [{ op: 'add', path: '/-', value: { id: 2 } }],
    );

    expect(merged, 'operations are de-duplicated by "op:path", so all "add /-" collapse into the last one').toHaveLength(2);
  });

  it('J3: keeps the order of the operations', () => {
    const operations = [
      { op: 'add', path: '/-', value: { id: 4 } },
      { op: 'replace', path: '/3/name', value: 'renamed' },
    ];

    expect(PatchUtils.mergeOperations(operations), 'operations are re-sorted by type, which changes their meaning').toEqual(operations);
  });
});
