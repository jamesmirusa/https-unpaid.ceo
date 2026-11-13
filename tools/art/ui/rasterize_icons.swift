// Rasterizes Phosphor SVG icons (MIT) to white-on-transparent PNGs for UI Toolkit (tinted in USS).
// usage: swift rasterize_icons.swift <phosphor assets dir> <out dir> <size px> name[:weight] ...
// weight: regular (default) | bold | fill. Output: <out>/<name>.png (a "-fill"/"-bold" suffix is kept in the name).
import AppKit

let args = CommandLine.arguments
guard args.count >= 5, let size = Int(args[3]) else {
    FileHandle.standardError.write("usage: rasterize_icons.swift <assets> <out> <size> name[:weight]...\n".data(using: .utf8)!)
    exit(2)
}
let assets = URL(fileURLWithPath: args[1]), out = URL(fileURLWithPath: args[2])
try? FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
var failed = 0
for spec in args[4...] {
    let parts = spec.split(separator: ":").map(String.init)
    let name = parts[0], weight = parts.count > 1 ? parts[1] : "regular"
    let file = weight == "regular" ? "\(name).svg" : "\(name)-\(weight).svg"
    let src = assets.appendingPathComponent(weight).appendingPathComponent(file)
    guard let svg = NSImage(contentsOf: src) else { print("missing \(src.path)"); failed += 1; continue }
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8,
                               samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                               bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    let rect = NSRect(x: 0, y: 0, width: size, height: size)
    svg.draw(in: rect)
    NSColor.white.set()
    rect.fill(using: .sourceAtop)   // keep the glyph's alpha, paint it white
    NSGraphicsContext.restoreGraphicsState()
    let outName = weight == "regular" ? name : "\(name)-\(weight)"
    try! rep.representation(using: .png, properties: [:])!.write(to: out.appendingPathComponent("\(outName).png"))
}
exit(failed == 0 ? 0 : 1)
