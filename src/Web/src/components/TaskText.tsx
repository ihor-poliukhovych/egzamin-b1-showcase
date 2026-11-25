import { Fragment, type ReactNode } from 'react'

const TOKEN = /(\{\d+\}|\[\[[^\]]+\]\]|\([^()]*\)\s*\[przykład\]|\[przykład\]|__[^_]+__|\n)/g

/**
 * Task text: {n} is gap no. n (rendered via renderSlot), [[word]] is a solved PRZYKŁAD,
 * "(a / b / c) [przykład]" is an example whose answer is not given in the key: options muted and a "przykład" label,
 * __part__ is an underlined part of the sentence, \n is a line break.
 */
export function TaskText({ text, renderSlot }: { text: string; renderSlot?: (n: number) => ReactNode }) {
  const parts = text.split(TOKEN)
  return (
    <p className="task-text">
      {parts.map((part, i) => {
        if (part === '\n') return <br key={i} />
        const slot = /^\{(\d+)\}$/.exec(part)
        if (slot) return <Fragment key={i}>{renderSlot ? renderSlot(Number(slot[1])) : part}</Fragment>
        const example = /^\[\[(.+)\]\]$/.exec(part)
        if (example) return <span key={i} className="example" title="PRZYKŁAD">{example[1]}</span>
        const choiceExample = /^(\([^()]*\))\s*\[przykład\]$/.exec(part)
        if (choiceExample) return <span key={i} className="example--choices" title="PRZYKŁAD">{choiceExample[1]}<span className="example__tag">przykład</span></span>
        if (part === '[przykład]') return <span key={i} className="example__tag">przykład</span>
        const underlined = /^__(.+)__$/.exec(part)
        if (underlined) return <u key={i}>{underlined[1]}</u>
        return <Fragment key={i}>{part}</Fragment>
      })}
    </p>
  )
}
