import { useEffect, useId, useState } from 'react'
import cursiveFontUrl from '@fontsource/playwrite-pl/files/playwrite-pl-latin-400-normal.woff2?url'
import { useI18n } from '../i18n'
import type { Lang } from '../i18n/core'

/**
 * Polish handwriting letters drawn with SVG lines (no fonts). Field 100×132 shifted up (viewBox from y=-12),
 * so marks fit above capitals: capital top y=14, lowercase top y=50, baseline y=90, descender bottom y=112.
 * Each "M…" in a path is a separate stroke in writing order; stroke direction follows school copybooks
 * (top to bottom, left to right, ovals counterclockwise). Marks (ogonek, kreska, kropka, the ł bar) are drawn last.
 */
const LOWER: Record<string, { d: string; dots?: [number, number][] }> = {
  a: { d: 'M63 60 A18 20 0 1 0 63 80 M63 52 V90' },
  b: { d: 'M30 12 V90 M30 70 A16 20 0 0 1 62 70 A16 20 0 0 1 30 70' },
  c: { d: 'M60 56 A18 20 0 1 0 60 84' },
  d: { d: 'M62 60 A18 20 0 1 0 62 80 M62 12 V90' },
  e: { d: 'M29 70 H62 A17 20 0 1 0 58 85' },
  f: { d: 'M60 18 Q53 10 46 13 Q40 17 40 28 V90 M28 52 H56' },
  g: { d: 'M63 60 A18 20 0 1 0 63 80 M63 52 V100 Q63 112 49 112 Q38 112 32 106' },
  h: { d: 'M32 12 V90 M32 64 Q46 46 59 58 V90' },
  i: { d: 'M46 52 V90', dots: [[46, 37]] },
  j: { d: 'M52 52 V102 Q52 112 41 112 Q33 112 29 106', dots: [[52, 37]] },
  k: { d: 'M32 12 V90 M60 52 L33 74 L61 90' },
  l: { d: 'M45 12 V83 Q45 90 53 90' },
  m: { d: 'M24 50 V90 M24 62 Q33 48 43 55 Q46 58 46 64 V90 M46 62 Q55 48 65 55 Q68 58 68 64 V90' },
  n: { d: 'M32 50 V90 M32 64 Q46 46 59 58 V90' },
  o: { d: 'M45 50 A17 20 0 0 0 45 90 A17 20 0 0 0 45 50' },
  p: { d: 'M30 50 V112 M30 70 A16 20 0 0 1 62 70 A16 20 0 0 1 30 70' },
  q: { d: 'M62 60 A18 20 0 1 0 62 80 M62 52 V112 Q63 106 70 104' },
  r: { d: 'M35 50 V90 M35 64 Q43 50 59 52' },
  s: { d: 'M60 56 Q46 45 33 54 Q26 64 45 70 Q64 76 58 86 Q46 96 29 86' },
  t: { d: 'M42 22 V82 Q42 90 52 90 Q58 90 61 86 M30 50 H57' },
  u: { d: 'M32 50 V76 Q32 90 46 90 Q59 90 59 76 M59 50 V90' },
  v: { d: 'M29 50 L46 90 L63 50' },
  w: { d: 'M20 50 L32 90 L45 60 L58 90 L70 50' },
  x: { d: 'M31 50 L61 90 M61 50 L31 90' },
  y: { d: 'M30 50 L46 88 M62 50 L40 107 Q36 113 29 111' },
  z: { d: 'M31 52 H60 L31 88 H62' },
}

const UPPER: Record<string, { d: string; dots?: [number, number][] }> = {
  A: { d: 'M46 14 L24 90 M46 14 L68 90 M32 64 H60' },
  B: { d: 'M30 14 V90 M30 14 H47 Q64 14 64 32 Q64 50 47 50 H30 M47 50 Q68 50 68 70 Q68 90 47 90 H30' },
  C: { d: 'M68 26 Q58 14 46 14 Q24 14 24 52 Q24 90 46 90 Q58 90 68 78' },
  D: { d: 'M28 14 V90 M28 14 H42 Q70 14 70 52 Q70 90 42 90 H28' },
  E: { d: 'M30 14 V90 M30 14 H62 M30 52 H56 M30 90 H64' },
  F: { d: 'M30 14 V90 M30 14 H62 M30 52 H56' },
  G: { d: 'M68 26 Q58 14 46 14 Q24 14 24 52 Q24 90 46 90 Q68 90 68 62 H50' },
  H: { d: 'M28 14 V90 M66 14 V90 M28 52 H66' },
  I: { d: 'M47 14 V90 M38 14 H56 M38 90 H56' },
  J: { d: 'M58 14 V72 Q58 90 42 90 Q30 90 26 80' },
  K: { d: 'M30 14 V90 M66 14 L31 58 M44 46 L68 90' },
  L: { d: 'M32 14 V90 H64' },
  M: { d: 'M22 90 V14 L46 62 L70 14 V90' },
  N: { d: 'M28 90 V14 L66 90 V14' },
  O: { d: 'M47 14 A23 38 0 0 0 47 90 A23 38 0 0 0 47 14' },
  P: { d: 'M30 14 V90 M30 14 H48 Q66 14 66 34 Q66 54 48 54 H30' },
  Q: { d: 'M47 14 A23 38 0 0 0 47 90 A23 38 0 0 0 47 14 M52 74 L72 96' },
  R: { d: 'M30 14 V90 M30 14 H48 Q66 14 66 34 Q66 54 48 54 H30 M46 54 L68 90' },
  S: { d: 'M66 24 Q56 12 44 14 Q26 16 28 32 Q30 46 47 52 Q66 58 66 74 Q64 92 44 90 Q30 89 24 80' },
  T: { d: 'M22 14 H70 M46 14 V90' },
  U: { d: 'M28 14 V64 Q28 90 47 90 Q66 90 66 64 V14' },
  V: { d: 'M24 14 L46 90 L68 14' },
  W: { d: 'M16 14 L30 90 L46 36 L62 90 L76 14' },
  X: { d: 'M26 14 L66 90 M66 14 L26 90' },
  Y: { d: 'M24 14 L46 52 L68 14 M46 52 V90' },
  Z: { d: 'M26 14 H66 L26 90 H68' },
}

type Mark = 'ogonek' | 'kreska' | 'kropka' | 'stroke'
interface Dia { base: string; mark: Mark; x?: number }

// letter with a mark → base + mark; ogonek at bottom right, tip pointing right; kreska is a short ↗; ł has a ↗ bar across the stem
const DIACRITIC: Record<string, Dia> = {
  ą: { base: 'a', mark: 'ogonek', x: 63 }, ę: { base: 'e', mark: 'ogonek', x: 54 },
  ć: { base: 'c', mark: 'kreska' }, ń: { base: 'n', mark: 'kreska' }, ó: { base: 'o', mark: 'kreska' },
  ś: { base: 's', mark: 'kreska' }, ź: { base: 'z', mark: 'kreska' }, ż: { base: 'z', mark: 'kropka' }, ł: { base: 'l', mark: 'stroke' },
  Ą: { base: 'A', mark: 'ogonek', x: 66 }, Ę: { base: 'E', mark: 'ogonek', x: 62 },
  Ć: { base: 'C', mark: 'kreska' }, Ń: { base: 'N', mark: 'kreska' }, Ó: { base: 'O', mark: 'kreska' },
  Ś: { base: 'S', mark: 'kreska' }, Ź: { base: 'Z', mark: 'kreska' }, Ż: { base: 'Z', mark: 'kropka' }, Ł: { base: 'L', mark: 'stroke' },
}

const ALPHABET = 'a ą b c ć d e ę f g h i j k l ł m n ń o ó p r s ś t u w y z ź ż'.split(' ')
const FOREIGN = ['q', 'v', 'x']
const DETAILED = ['ą', 'ę', 'ó', 'ś', 'ć', 'ź', 'ń', 'ż', 'ł']

const isUpper = (ch: string) => ch !== ch.toLowerCase()

/** Mark: for lowercase, above x-height; for capitals, above cap height. */
function markShape(mark: Mark, upper: boolean, x = 60): { d?: string; dot?: [number, number] } {
  if (mark === 'ogonek') return { d: `M${x} 89 Q${x - 7} 98 ${x - 3} 105 Q${x + 1} 110 ${x + 8} 106` }
  if (mark === 'kreska') return { d: upper ? 'M42 6 L56 -6' : 'M41 41 L55 28' }
  if (mark === 'kropka') return { dot: upper ? [46, 1] : [46, 36] }
  return { d: upper ? 'M20 62 L44 44' : 'M34 62 L57 44' }
}

/** Start point of each stroke: the first two numbers after each "M". */
function strokeStarts(d: string): [number, number][] {
  return d.split('M').filter((s) => s.trim()).map((s) => {
    const [x, y] = s.trim().split(/[\s,A-Za-z]+/).map(Number)
    return [x, y]
  })
}

export function Glyph({ ch, className = '', detailed = false }: { ch: string; className?: string; detailed?: boolean }) {
  const id = useId().replace(/:/g, '')
  const dia = DIACRITIC[ch]
  const baseCh = dia ? dia.base : ch
  const upper = isUpper(baseCh)
  const base = (upper ? UPPER : LOWER)[baseCh]
  if (!base) return null
  const mark = dia ? markShape(dia.mark, upper, dia.x) : null
  const strokes = base.d.split('M').filter((s) => s.trim()).map((s) => `M${s}`)
  const starts = strokeStarts(base.d)
  const dots = base.dots ?? []
  // mark / dot number goes after all the strokes of the letter
  let n = strokes.length

  return (
    <svg viewBox="0 -12 100 132" className={`letter-glyph ${className}`} aria-hidden="true">
      {detailed && (
        <defs>
          <marker id={`arr${id}`} viewBox="0 0 10 10" refX="6" refY="5" markerWidth="5" markerHeight="5" orient="auto-start-reverse">
            <path d="M0 0 L10 5 L0 10 z" className="letter-glyph__arrow" />
          </marker>
        </defs>
      )}
      {upper && <line x1="10" y1="14" x2="90" y2="14" className="letter-glyph__guide" />}
      <line x1="10" y1="50" x2="90" y2="50" className="letter-glyph__guide" />
      <line x1="10" y1="90" x2="90" y2="90" className="letter-glyph__guide letter-glyph__guide--base" />
      {strokes.map((d, i) => <path key={i} d={d} className="letter-glyph__stroke" markerEnd={detailed ? `url(#arr${id})` : undefined} />)}
      {dots.map(([cx, cy]) => <circle key={`d${cx}`} cx={cx} cy={cy} r="4.5" className="letter-glyph__ink" />)}
      {mark?.d && <path d={mark.d} className="letter-glyph__mark" />}
      {mark?.dot && <circle cx={mark.dot[0]} cy={mark.dot[1]} r="4.5" className="letter-glyph__dot" />}
      {detailed && starts.map(([x, y], i) => (
        <g key={`n${i}`} className="letter-glyph__num">
          <circle cx={x} cy={y} r="7" />
          <text x={x} y={y + 3.6} textAnchor="middle">{i + 1}</text>
        </g>
      ))}
      {detailed && dots.map(([cx, cy]) => {
        n += 1
        return <g key={`dn${cx}`} className="letter-glyph__num"><circle cx={cx + 12} cy={cy - 4} r="7" /><text x={cx + 12} y={cy - 0.4} textAnchor="middle">{n}</text></g>
      })}
      {detailed && mark && (() => {
        n += 1
        const [x, y] = mark.dot ?? strokeStarts(mark.d!)[0]
        return <g className="letter-glyph__num letter-glyph__num--mark"><circle cx={x + 13} cy={y} r="7" /><text x={x + 13} y={y + 3.6} textAnchor="middle">{n}</text></g>
      })()}
    </svg>
  )
}

const TEXT: Record<Lang, {
  alphabetTitle: string; alphabetNote: string; foreignNote: string; pick: string; howTo: string; markLast: string; legend: string
  title: string; marks: Record<Mark, string>; mistakesTitle: string; mistakes: [string, string][]
}> = {
  uk: {
    alphabetTitle: 'Польська абетка — 32 літери', alphabetNote: 'Виділені знаки — те, чого немає в кирилиці.', foreignNote: 'q, v, x — лише в іншомовних словах',
    pick: 'Натисніть на літеру, щоб побачити, як її писати', howTo: 'Як писати', markLast: 'Знак ставлять останнім, коли літеру вже написано.',
    legend: 'Цифра — порядок рисок, стрілка — напрямок.',
    title: 'Як правильно писати польські літери від руки',
    marks: { ogonek: 'ogonek: справа внизу, кінчик дивиться вправо', kreska: 'kreska: коротка риска навскіс ↗', kropka: 'kropka: крапка просто над літерою', stroke: 'ł: риска навскіс ↗ через паличку' },
    mistakesTitle: 'Часті помилки',
    mistakes: [['ogonek ліворуч, як у ç', 'ogonek — завжди праворуч'], ['крапка замість риски над ó', 'ó, ś, ć, ź, ń — коротка риска ↗'], ['ż і ź плутають', 'ż — крапка (żaba), ź — риска (źle)'], ['горизонтальна риска в ł — схоже на t', 'риска в ł — навскіс ↗']],
  },
  ru: {
    alphabetTitle: 'Польский алфавит — 32 буквы', alphabetNote: 'Выделенные знаки — то, чего нет в кириллице.', foreignNote: 'q, v, x — только в иностранных словах',
    pick: 'Нажмите на букву, чтобы увидеть, как её писать', howTo: 'Как писать', markLast: 'Знак ставят последним, когда буква уже написана.',
    legend: 'Цифра — порядок черт, стрелка — направление.',
    title: 'Как правильно писать польские буквы от руки',
    marks: { ogonek: 'ogonek: справа внизу, кончик смотрит вправо', kreska: 'kreska: короткая чёрточка наискосок ↗', kropka: 'kropka: точка прямо над буквой', stroke: 'ł: черта наискосок ↗ через палочку' },
    mistakesTitle: 'Частые ошибки',
    mistakes: [['ogonek влево, как у ç', 'ogonek — всегда справа'], ['точка вместо черты над ó', 'ó, ś, ć, ź, ń — короткая черта ↗'], ['путают ż и ź', 'ż — точка (żaba), ź — черта (źle)'], ['горизонтальная черта в ł — похоже на t', 'черта в ł — наискосок ↗']],
  },
  pl: {
    alphabetTitle: 'Polski alfabet — 32 litery', alphabetNote: 'Wyróżnione znaki to to, czego nie ma w cyrylicy.', foreignNote: 'q, v, x — tylko w słowach obcych',
    pick: 'Kliknij literę, żeby zobaczyć, jak ją pisać', howTo: 'Jak pisać', markLast: 'Znak dopisuje się na końcu, gdy litera jest już napisana.',
    legend: 'Cyfra — kolejność kresek, strzałka — kierunek.',
    title: 'Jak poprawnie pisać polskie litery odręcznie',
    marks: { ogonek: 'ogonek: z prawej na dole, końcówka w prawo', kreska: 'kreska: krótka, ukośna ↗', kropka: 'kropka: dokładnie nad literą', stroke: 'ł: ukośna kreska ↗ przez laseczkę' },
    mistakesTitle: 'Częste błędy',
    mistakes: [['ogonek w lewo, jak w ç', 'ogonek — zawsze z prawej'], ['kropka zamiast kreski nad ó', 'ó, ś, ć, ź, ń — krótka kreska ↗'], ['mylenie ż i ź', 'ż — kropka (żaba), ź — kreska (źle)'], ['pozioma kreska w ł — jak t', 'kreska w ł — ukośna ↗']],
  },
  en: {
    alphabetTitle: 'The Polish alphabet — 32 letters', alphabetNote: 'Highlighted marks are what Cyrillic does not have.', foreignNote: 'q, v, x — only in foreign words',
    pick: 'Tap a letter to see how to write it', howTo: 'How to write', markLast: 'The mark is added last, after the letter itself.',
    legend: 'Numbers show the stroke order, arrows the direction.',
    title: 'How to handwrite Polish letters correctly',
    marks: { ogonek: 'ogonek: bottom right, tip pointing right', kreska: 'kreska: short slanted stroke ↗', kropka: 'kropka: a dot right above the letter', stroke: 'ł: slanted stroke ↗ through the stem' },
    mistakesTitle: 'Common mistakes',
    mistakes: [['ogonek to the left, like ç', 'the ogonek is always on the right'], ['a dot instead of a stroke on ó', 'ó, ś, ć, ź, ń — a short stroke ↗'], ['mixing up ż and ź', 'ż — dot (żaba), ź — stroke (źle)'], ['horizontal bar in ł — looks like t', 'the bar in ł is slanted ↗']],
  },
}

/** Large pair "Ą ą" with stroke order and direction. */
function LetterDetail({ ch }: { ch: string }) {
  const { lang } = useI18n()
  const text = TEXT[lang]
  const dia = DIACRITIC[ch]
  return (
    <div className="letter-detail" aria-live="polite">
      <div className="letter-detail__glyphs" lang="pl">
        <Glyph ch={ch.toUpperCase()} detailed className="letter-glyph--detail" />
        <Glyph ch={ch} detailed className="letter-glyph--detail" />
      </div>
      <div className="letter-detail__text">
        <strong className="letter-detail__title">{text.howTo}: <span lang="pl">{ch.toUpperCase()} {ch}</span></strong>
        <span className="muted small">{text.legend}</span>
        {dia && <span className="small">{text.marks[dia.mark]}. {text.markLast}</span>}
      </div>
    </div>
  )
}

// school handwriting font Playwrite Polska (OFL): loaded only with the knowledge base, no requests to Google
let cursiveFontRequested = false
function ensureCursiveFont() {
  if (cursiveFontRequested || typeof FontFace === 'undefined') return
  cursiveFontRequested = true
  const face = new FontFace('Playwrite PL School', `url(${cursiveFontUrl})`)
  void face.load().then((f) => { document.fonts.add(f) }).catch(() => { /* the fallback handwriting font stays */ })
}

/** Example word for each letter: the letter is highlighted in it, as in a school copybook. */
const WORDS: Record<string, string> = {
  a: 'Ala', ą: 'wąż', b: 'brat', c: 'cena', ć: 'ćma', d: 'dom', e: 'ekran', ę: 'ręka', f: 'foka', g: 'góra', h: 'herbata',
  i: 'Ile', j: 'Jak', k: 'kiedy', l: 'Lidia', ł: 'Ławka', m: 'Marta', n: 'noc', ń: 'koń', o: 'okno', ó: 'ósemka', p: 'pies',
  r: 'razem', s: 'sok', ś: 'ściana', t: 'tata', u: 'ucho', w: 'woda', y: 'ryby', z: 'Zofia', ź: 'źle', ż: 'żaba',
  q: 'quiz', v: 'video', x: 'taxi',
}

/** Letters that are written by hand differently from print. */
const CURSIVE_NOTES: Record<Lang, Record<string, string>> = {
  uk: {
    i: 'Велике I від руки пишуть з петелькою вгорі — воно схоже на J, але стоїть на лінії й не опускається нижче.',
    j: 'Велике J від руки опускається нижче рядка петлею. Не плутайте з I: I залишається на лінії.',
    ł: 'Ł — рукописна L з косою рискою ↗ через ствол. Без риски це вже L (Lidia), з рискою — Ł (Ławka).',
  },
  ru: {
    i: 'Заглавное I от руки пишут с петелькой сверху — оно похоже на J, но стоит на строке и вниз не уходит.',
    j: 'Заглавное J от руки уходит ниже строки петлёй. Не путайте с I: I остаётся на строке.',
    ł: 'Ł — рукописная L с косой чертой ↗ через ствол. Без черты это уже L (Lidia), с чертой — Ł (Ławka).',
  },
  pl: {
    i: 'Wielkie I pisane ma pętelkę u góry — przypomina J, ale stoi na linii i nie schodzi niżej.',
    j: 'Wielkie J pisane schodzi pętelką poniżej linii. Nie myl z I: I zostaje na linii.',
    ł: 'Ł to pisane L z ukośną kreską ↗ przez laseczkę. Bez kreski to L (Lidia), z kreską — Ł (Ławka).',
  },
  en: {
    i: 'Handwritten capital I has a loop at the top — it looks like J, but it sits on the line and does not go below it.',
    j: 'Handwritten capital J drops below the line in a loop. Do not mix it up with I, which stays on the line.',
    ł: 'Ł is a handwritten L with a slanted stroke ↗ through the stem. Without the stroke it is L (Lidia), with it — Ł (Ławka).',
  },
}

const MODE_TEXT: Record<Lang, { cursive: string; print: string; cursiveHint: string }> = {
  uk: { cursive: 'Від руки', print: 'Друковані', cursiveHint: 'Так пишуть у польській школі. На іспиті можна писати й друкованими — головне, щоб знаки були чіткі.' },
  ru: { cursive: 'От руки', print: 'Печатные', cursiveHint: 'Так пишут в польской школе. На экзамене можно писать и печатными — главное, чтобы знаки были чёткими.' },
  pl: { cursive: 'Pisane', print: 'Drukowane', cursiveHint: 'Tak pisze się w polskiej szkole. Na egzaminie można pisać też drukowanymi — ważne, żeby znaki były wyraźne.' },
  en: { cursive: 'Handwritten', print: 'Print', cursiveHint: 'This is how Polish schools teach handwriting. In the exam you may also print — just keep the marks clear.' },
}

/** A word where the target letter is red, like the first letter in a copybook. */
function CursiveWord({ word, ch }: { word: string; ch: string }) {
  return <>{[...word].map((c, i) => <span key={i} className={c.toLowerCase() === ch ? 'cursive__hit' : undefined}>{c}</span>)}</>
}

/** Handwritten view: the pair "Aa" and a word on ruled lines, like in a school notebook. */
function CursiveSheet({ picked, onPick }: { picked: string; onPick: (ch: string) => void }) {
  const { lang } = useI18n()
  const note = CURSIVE_NOTES[lang][picked]
  return (
    <>
      <div className="cursive-detail">
        <div className="cursive cursive--big" lang="pl"><span className="cursive__hit">{picked.toUpperCase()}</span>{picked}</div>
        <div className="cursive cursive--word" lang="pl"><CursiveWord word={WORDS[picked]} ch={picked} /></div>
        {note && <p className="cursive-detail__note">{note}</p>}
      </div>
      <ul className="cursive-grid" lang="pl">
        {[...ALPHABET, ...FOREIGN].map((ch) => (
          <li key={ch}>
            <button type="button" aria-pressed={picked === ch} onClick={() => onPick(ch)}
              className={`cursive-row ${picked === ch ? 'cursive-row--on' : ''} ${FOREIGN.includes(ch) ? 'cursive-row--foreign' : ''} ${CURSIVE_NOTES[lang][ch] ? 'cursive-row--note' : ''}`}>
              <span className="cursive cursive--pair"><span className="cursive__hit">{ch.toUpperCase()}</span>{ch}</span>
              <span className="cursive cursive--small"><CursiveWord word={WORDS[ch]} ch={ch} /></span>
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

/** [[alfabet]] — the whole alphabet: handwritten (school copybook) or printed with stroke order. */
export function AlphabetSheet() {
  const { lang } = useI18n()
  const text = TEXT[lang]
  const mode = MODE_TEXT[lang]
  const [picked, setPicked] = useState('a')
  const [view, setView] = useState<'cursive' | 'print'>('cursive')
  useEffect(ensureCursiveFont, [])
  return (
    <figure className="letter-sheet" role="group" aria-label={`${text.alphabetTitle}: ${ALPHABET.join(' ')}`}>
      <figcaption className="letter-sheet__title">{text.alphabetTitle}</figcaption>
      <div className="segmented letter-sheet__modes" role="group">
        <button type="button" aria-pressed={view === 'cursive'} className={`segmented__btn ${view === 'cursive' ? 'segmented__btn--on' : ''}`} onClick={() => setView('cursive')}>{mode.cursive}</button>
        <button type="button" aria-pressed={view === 'print'} className={`segmented__btn ${view === 'print' ? 'segmented__btn--on' : ''}`} onClick={() => setView('print')}>{mode.print}</button>
      </div>
      {view === 'cursive' ? (
        <>
          <p className="letter-sheet__note">{mode.cursiveHint}</p>
          <CursiveSheet picked={picked} onPick={setPicked} />
          <p className="letter-sheet__note">{text.pick}. {text.foreignNote}.</p>
        </>
      ) : (
        <PrintSheet picked={picked} onPick={setPicked} />
      )}
    </figure>
  )
}

/** Printed letters: a large pair with stroke order and direction + a grid of the whole alphabet. */
function PrintSheet({ picked, onPick }: { picked: string; onPick: (ch: string) => void }) {
  const { lang } = useI18n()
  const text = TEXT[lang]
  const setPicked = onPick
  return (
    <>
      <LetterDetail ch={picked} />
      <p className="letter-sheet__note">{text.pick}</p>
      <ul className="alphabet-grid" lang="pl">
        {[...ALPHABET, ...FOREIGN].map((ch) => (
          <li key={ch}>
            <button type="button" aria-pressed={picked === ch} onClick={() => setPicked(ch)}
              className={`alphabet-grid__item ${DIACRITIC[ch] ? 'alphabet-grid__item--special' : ''} ${FOREIGN.includes(ch) ? 'alphabet-grid__item--foreign' : ''} ${picked === ch ? 'alphabet-grid__item--on' : ''}`}>
              <span className="alphabet-grid__pair">
                <Glyph ch={ch.toUpperCase()} />
                <Glyph ch={ch} />
              </span>
              <span className="alphabet-grid__label">{ch.toUpperCase()} {ch}</span>
            </button>
          </li>
        ))}
      </ul>
      <p className="letter-sheet__note">{text.alphabetNote} {text.foreignNote}.</p>
    </>
  )
}

/** [[litery]] — details on letters with marks and common mistakes. */
export function LetterSheet() {
  const { lang } = useI18n()
  const text = TEXT[lang]
  return (
    <figure className="letter-sheet" role="group" aria-label={`${text.title}: ${DETAILED.join(' ')}`}>
      <figcaption className="letter-sheet__title">{text.title}</figcaption>
      <ul className="letter-sheet__grid">
        {DETAILED.map((ch) => (
          <li key={ch} className="letter-sheet__item">
            <span className="alphabet-grid__pair" lang="pl">
              <Glyph ch={ch.toUpperCase()} detailed className="letter-glyph--big" />
              <Glyph ch={ch} detailed className="letter-glyph--big" />
            </span>
            <strong className="letter-sheet__ch" lang="pl">{ch.toUpperCase()} {ch}</strong>
            <span className="letter-sheet__hint">{text.marks[DIACRITIC[ch].mark]}</span>
          </li>
        ))}
      </ul>
      <p className="letter-sheet__note">{text.legend} {text.markLast}</p>
      <p className="letter-sheet__subtitle">{text.mistakesTitle}</p>
      <ul className="letter-sheet__mistakes">
        {text.mistakes.map(([wrong, right]) => (
          <li key={wrong}><span className="md__wrong">✗</span> {wrong} <span className="md__right">✓</span> {right}</li>
        ))}
      </ul>
    </figure>
  )
}
