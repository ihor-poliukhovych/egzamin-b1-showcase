import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const langs = ['uk', 'ru', 'pl', 'en']
const dict = Object.fromEntries(langs.map((l) => [l, JSON.parse(readFileSync(new URL(`../src/i18n/${l}.json`, import.meta.url), 'utf8'))]))
const params = (text) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',')

test('uk, ru, pl and en dictionaries have exactly the same keys', () => {
  const reference = Object.keys(dict.uk).sort()
  for (const lang of langs) {
    const keys = Object.keys(dict[lang]).sort()
    assert.deepEqual(keys.filter((k) => !reference.includes(k)), [], `${lang}: extra keys`)
    assert.deepEqual(reference.filter((k) => !keys.includes(k)), [], `${lang}: missing keys`)
  }
})

test('no translation is empty (except intentional separators)', () => {
  for (const lang of langs) {
    for (const [key, value] of Object.entries(dict[lang])) {
      assert.equal(typeof value, 'string', `${lang}.${key} must be a string`)
      assert.ok(value.trim() !== '' || value === '', `${lang}.${key} is blank`)
      assert.ok(value !== '' , `${lang}.${key} is empty`)
    }
  }
})

test('every translation uses the same {placeholders} as the Ukrainian source', () => {
  for (const [key, value] of Object.entries(dict.uk)) {
    for (const lang of ['ru', 'pl', 'en']) {
      assert.equal(params(dict[lang][key]), params(value), `${lang}.${key}: placeholders differ`)
    }
  }
})

test('plural groups are complete: one, few, many and other exist in every language', () => {
  const bases = new Set(Object.keys(dict.uk).filter((k) => k.endsWith('_other')).map((k) => k.slice(0, -'_other'.length)))
  assert.ok(bases.size > 0)
  for (const lang of langs) {
    for (const base of bases) {
      for (const form of ['one', 'few', 'many', 'other']) {
        assert.ok(`${base}_${form}` in dict[lang], `${lang}: ${base}_${form} is missing`)
      }
    }
  }
})

test('Ukrainian and Polish texts are not left in Russian', () => {
  // letters absent from Ukrainian (Russian-only ones, see the regex below); Polish must have no Cyrillic at all
  for (const [key, value] of Object.entries(dict.uk)) assert.ok(!/[\u044B\u044D\u044A\u0451]/i.test(value), `uk.${key} looks Russian`)
  for (const lang of ['pl', 'en']) {
    for (const [key, value] of Object.entries(dict[lang])) assert.ok(!/[\u0400-\u04FF]/.test(value), `${lang}.${key} contains Cyrillic`)
  }
})
