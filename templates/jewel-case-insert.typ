// k7mix-title: Cassette jewel-case insert
// Printed at actual size. Cut on the crop marks, fold on the spine and flaps,
// and slide the card into a compact-cassette jewel case. The cassette labels
// on page 2 sit on the shell, not in the case.

#let data = json("mix.json")

#let front-w = 100mm
#let spine-w = 12.5mm
#let back-w = 70mm
#let face-h = 64mm
#let flap = 10mm
#let bleed = 3mm
#let safe = 3mm
#let card-w = front-w + spine-w + back-w
#let card-h = face-h + flap * 2

#let tape = rgb(data.at("tapeColor", default: "#1E1E22"))
#let sticker = rgb(data.at("stickerColor", default: "#CDBA9E"))
#let ink = rgb(data.at("textColor", default: "#000000"))
#let font-id = data.at("font", default: "roboto")
#let title-font = if font-id == "cousine" { "Cousine" } else if font-id == "rock-salt" { "Rock Salt" } else if font-id == "fixedsys" { "Fixedsys Excelsior 3.01" } else { "Roboto" }

#set page(width: 210mm, height: 297mm, margin: 12mm)
#set text(font: "Roboto", size: 10pt, fill: luma(12%))

#let crop-marks(w, h) = {
  let arm = 4mm
  let gap = 1.5mm
  place(dx: -arm - gap, dy: 0mm, line(length: arm, stroke: 0.2pt))
  place(dx: 0mm, dy: -arm - gap, line(length: arm, angle: 90deg, stroke: 0.2pt))
  place(dx: w + gap, dy: 0mm, line(length: arm, stroke: 0.2pt))
  place(dx: w, dy: -arm - gap, line(length: arm, angle: 90deg, stroke: 0.2pt))
  place(dx: -arm - gap, dy: h, line(length: arm, stroke: 0.2pt))
  place(dx: 0mm, dy: h + gap, line(length: arm, angle: 90deg, stroke: 0.2pt))
  place(dx: w + gap, dy: h, line(length: arm, stroke: 0.2pt))
  place(dx: w, dy: h + gap, line(length: arm, angle: 90deg, stroke: 0.2pt))
}

#let fold-guide(x, h) = {
  place(dx: x, dy: -2mm, line(length: h + 4mm, angle: 90deg, stroke: (paint: luma(55%), thickness: 0.2pt, dash: "dotted")))
}

#let panel(fill, body) = {
  block(width: 100%, height: 100%, fill: fill, inset: safe, body)
}

#align(center)[
  #text(font: "Roboto Slab", size: 16pt)[K7 Mix insert]
  #linebreak()
  #text(size: 9pt, font: ("Roboto Condensed", "Roboto"))[Print at 100 %. Cut on the marks. Fold the spine and the top and bottom flaps.]
]

#v(8mm)

#align(center, {
  box(width: card-w, height: card-h, {
    crop-marks(card-w, card-h)
    fold-guide(front-w, card-h)
    fold-guide(front-w + spine-w, card-h)
    fold-guide(0mm, flap)
    // flaps
    place(dx: 0mm, dy: 0mm, rect(width: card-w, height: flap, fill: tape.lighten(70%), stroke: none))
    place(dx: 0mm, dy: flap + face-h, rect(width: card-w, height: flap, fill: tape.lighten(70%), stroke: none))
    // front
    place(dx: 0mm, dy: flap, {
      rect(width: front-w, height: face-h, fill: tape, stroke: none)
      if data.at("front", default: none) != none {
        place(image(data.at("front"), width: front-w, height: face-h))
      } else {
        place(center + horizon, text(font: title-font, size: 14pt, fill: ink, data.at("title", default: "")))
      }
    })
    // spine
    place(dx: front-w, dy: flap, {
      rect(width: spine-w, height: face-h, fill: tape.darken(10%), stroke: none)
      rotate(-90deg, reflow: true, text(font: title-font, size: 8pt, fill: ink, data.at("title", default: "")))
    })
    // back
    place(dx: front-w + spine-w, dy: flap, {
      rect(width: back-w, height: face-h, fill: sticker, stroke: none)
      if data.at("back", default: none) != none {
        place(image(data.at("back"), width: back-w, height: face-h))
      } else {
        block(inset: safe, {
          set text(font: "Roboto", size: 7pt, fill: ink)
          text(font: title-font, size: 9pt, data.at("title", default: ""))
          linebreak()
          text(data.at("description", default: ""))
        })
      }
    })
  })
})

#v(8mm)
#block(width: 160mm)[
  #set text(size: 9pt)
  Jewel-case insert, not the cassette sticker. Front #front-w, spine #spine-w, back #back-w, face height #face-h, flaps #flap, safe area #safe. #data.at("notice", default: "")
]

#pagebreak()

#align(center)[
  #text(font: "Roboto Slab", size: 16pt)[Cassette labels]
  #linebreak()
  #text(size: 9pt, font: ("Roboto Condensed", "Roboto"))[Side A and side B. Cut and stick on the cassette shell.]
]

#v(10mm)

#let cassette-label(side, caption) = {
  box(width: 89mm, height: 43mm, {
    crop-marks(89mm, 43mm)
    rect(width: 89mm, height: 43mm, fill: sticker, stroke: 0.4pt + tape)
    place(dx: 4mm, dy: 5mm, text(font: ("Roboto Condensed", "Roboto"), size: 8pt, fill: ink, "Side " + side))
    place(center + horizon, text(font: title-font, size: 13pt, fill: ink, caption))
  })
}

#align(center, stack(
  dir: ltr,
  spacing: 16mm,
  cassette-label("A", data.at("sideA", default: "")),
  cassette-label("B", data.at("sideB", default: "")),
))

#v(12mm)
#text(font: "Roboto Slab", size: 12pt)[Track list]
#table(
  columns: (12mm, 12mm, 1fr, 28mm, 22mm),
  stroke: 0.3pt + luma(80%),
  inset: 4pt,
  [*No.*], [*Side*], [*Title*], [*Codename*], [*Length*],
  ..data.at("tracks", default: ()).map(t => (
    [#t.at("n")],
    t.at("side"),
    t.at("title"),
    text(font: "Roboto Mono", size: 8pt, t.at("codename", default: "")),
    t.at("duration"),
  )).flatten()
)
