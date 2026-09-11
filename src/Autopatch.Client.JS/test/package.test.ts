import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { PatchUtils } from '../src/utils/patch-utils';

const packageDirectory = join(dirname(fileURLToPath(import.meta.url)), '..');
const packageJson = JSON.parse(readFileSync(join(packageDirectory, 'package.json'), 'utf8'));
const major = (range: string) => Number(/\d+/.exec(range)?.[0]);

describe('J3 package metadata', () => {
  it('J3: react-dom is not a runtime dependency', () => {
    expect(
      Object.keys(packageJson.dependencies ?? {}),
      'every consumer installs react-dom although react is an optional peer dependency',
    ).not.toContain('react-dom');
  });

  it('J3: @types/react matches the supported react version', () => {
    expect(major(packageJson.devDependencies['@types/react']), 'the typings are for another major version than the peer dependency').toBe(
      major(packageJson.peerDependencies.react),
    );
  });

  it('J3: repository.directory points to the package', () => {
    expect(packageJson.repository.directory).toBe('src/Autopatch.Client.JS');
  });

  it('J3: build output is not committed', () => {
    const trackedBuildOutput = execSync('git ls-files dist', { cwd: packageDirectory, encoding: 'utf8' }).trim();

    expect(trackedBuildOutput, 'dist/ is checked in and can get out of sync with src/').toBe('');
  });
});

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
