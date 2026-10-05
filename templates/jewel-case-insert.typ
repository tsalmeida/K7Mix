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
#let spine-ink = rgb(data.at("spineInk", default: "#FFFFFF"))
#let font-id = data.at("font", default: "roboto")
// The four bundled faces by id; a standard Logarktos face by its family name, falling back to Roboto.
#let title-font = if font-id == "cousine" { "Cousine" } else if font-id == "rock-salt" { "Rock Salt" } else if font-id == "fixedsys" { "Fixedsys Excelsior" } else if font-id == "roboto" { "Roboto" } else { (data.at("fontFamily", default: "Roboto"), "Roboto") }

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

#let fold-stroke = (paint: luma(55%), thickness: 0.2pt, dash: "dotted")
#let vfold(x) = place(dx: x, dy: -2mm, line(length: card-h + 4mm, angle: 90deg, stroke: fold-stroke))
#let hfold(y) = place(dx: -2mm, dy: y, line(length: card-w + 4mm, stroke: fold-stroke))

// Every panel is a clipped box. Typst does not clip overflow on its own, so an
// image scaled to cover its panel spills across the rest of the card and off the
// page. Clipping here is what turns "cover" into the same centre crop that the
// app previews and the package stores.
#let panel(w, bg, body) = box(width: w, height: face-h, fill: bg, clip: true, body)

#let art-panel(w, path, fallback) = panel(w, sticker, {
  if path != none {
    image(path, width: 100%, height: 100%, fit: "cover")
  } else {
    fallback
  }
})

#let mm-of(len) = str(calc.round(len / 1mm, digits: 2)) + " mm"

#align(center)[
  #text(font: "Roboto Slab", size: 16pt)[K7 Mix insert]
  #linebreak()
  #text(size: 9pt, font: ("Roboto Condensed", "Roboto"))[Print at 100 %. Cut on the marks. Fold the spine and the top and bottom flaps.]
]

#v(8mm)

#align(center, {
  box(width: card-w, height: card-h, {
    // flaps
    place(dx: 0mm, dy: 0mm, rect(width: card-w, height: flap, fill: tape.lighten(70%), stroke: none))
    place(dx: 0mm, dy: flap + face-h, rect(width: card-w, height: flap, fill: tape.lighten(70%), stroke: none))

    // front — printed paper, so the sticker colour rather than the tape colour,
    // which used to leave the title as dark ink on a dark panel
    place(dx: 0mm, dy: flap, art-panel(
      front-w,
      data.at("front", default: none),
      align(center + horizon, text(font: title-font, size: 14pt, fill: ink, data.at("title", default: ""))),
    ))

    // spine — the one dark panel, so its text takes a colour that reads on it.
    // The inner box is given the length of the spine before the rotation, or the
    // title wraps to the 12.5 mm panel width and comes out stacked letter by
    // letter instead of running along the spine.
    place(dx: front-w, dy: flap, panel(spine-w, tape, align(center + horizon, rotate(
      -90deg,
      reflow: true,
      box(
        width: face-h - 2 * safe,
        align(center, text(
          font: title-font,
          size: 8pt,
          fill: spine-ink,
          data.at("title", default: ""),
        )),
      ),
    ))))

    // back
    place(dx: front-w + spine-w, dy: flap, art-panel(
      back-w,
      data.at("back", default: none),
      block(inset: safe, {
        set text(font: "Roboto", size: 7pt, fill: ink)
        text(font: title-font, size: 9pt, data.at("title", default: ""))
        linebreak()
        text(data.at("description", default: ""))
      }),
    ))

    crop-marks(card-w, card-h)
    vfold(front-w)
    vfold(front-w + spine-w)
    hfold(flap)
    hfold(flap + face-h)
  })
})

#v(8mm)
#block(width: 160mm)[
  #set text(size: 9pt)
  Jewel-case insert, not the cassette sticker. Front #mm-of(front-w), spine #mm-of(spine-w),
  back #mm-of(back-w), face height #mm-of(face-h), flaps #mm-of(flap), safe area #mm-of(safe).
  Artwork is cropped from the middle to the shape of the panel it prints on.
  #data.at("notice", default: "")
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
    // Placed, not in flow: a rect in the flow pushes everything after it down, so
    // the side letter used to print below the label instead of inside it.
    place(dx: 0mm, dy: 0mm, rect(width: 89mm, height: 43mm, fill: sticker, stroke: 0.4pt + tape))
    place(dx: 4mm, dy: 4mm, text(font: ("Roboto Condensed", "Roboto"), size: 8pt, fill: ink, "Side " + side))
    place(center + horizon, text(font: title-font, size: 13pt, fill: ink, caption))
    crop-marks(89mm, 43mm)
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
