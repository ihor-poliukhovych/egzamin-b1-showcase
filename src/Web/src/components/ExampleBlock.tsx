import { TaskText } from './TaskText'
import { parseChoiceExample, TRAILING_VALUE } from '../exampleParse'

/** A task's example shown as a solved item: question, options in a column, the correct one marked; nothing is clickable. */
export function ExampleBlock({ example }: { example: string }) {
  const choice = parseChoiceExample(example)
  const value = choice ? null : TRAILING_VALUE.exec(example)

  return (
    <div className="example-block" aria-label="PRZYKŁAD">
      <span className="step__label">PRZYKŁAD</span>
      {choice ? (
        <div className="example-q">
          <p className="example-q__prompt">{choice.prompt}</p>
          <ul className="example-q__choices">
            {choice.choices.map((c) => (
              <li key={c.letter} className={`example-q__choice ${c.letter === choice.answer ? 'example-q__choice--on' : ''}`}>
                <span className="example-q__letter" aria-hidden="true">{c.letter}</span>
                <span>{c.text}</span>
                {c.letter === choice.answer && <span className="example-q__mark" aria-label="poprawna odpowiedź">✓</span>}
              </li>
            ))}
          </ul>
        </div>
      ) : value ? (
        <p className="example-q__statement">
          <span>{value[1]}</span>
          <span className="example-q__answer">{value[2]} ✓</span>
        </p>
      ) : (
        <TaskText text={example} />
      )}
    </div>
  )
}
