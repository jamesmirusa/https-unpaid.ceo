// Slices a transparent N×N icon sheet into square PNGs: each cell is trimmed to its opaque pixels, centered on a
// square canvas with a small margin and scaled to <size> px.
// usage: swift slice_sheet.swift <sheet.png> <grid N> <size px> <out dir> name1 name2 ... (row by row; "-" skips a cell)
import AppKit

let a = CommandLine.arguments
guard a.count >= 6, let grid = Int(a[2]), let size = Int(a[3]) else { print("usage"); exit(2) }
guard let img = NSImage(contentsOfFile: a[1]), let cg = img.cgImage(forProposedRect: nil, context: nil, hints: nil) else { print("bad sheet"); exit(1) }
let out = URL(fileURLWithPath: a[4])
try? FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
let W = cg.width, H = cg.height, cw = W / grid, ch = H / grid
// RGBA bytes of the whole sheet
var px = [UInt8](repeating: 0, count: W * H * 4)
let ctx = CGContext(data: &px, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W * 4,
                    space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(cg, in: CGRect(x: 0, y: 0, width: W, height: H))
let names = Array(a[5...])
for (i, name) in names.enumerated() where name != "-" {
    let col = i % grid, row = i / grid
    // memory rows go top-down after drawing into a bitmap context
    var minX = Int.max, minY = Int.max, maxX = -1, maxY = -1
    for y in (row * ch)..<((row + 1) * ch) { for x in (col * cw)..<((col + 1) * cw) {
        if px[(y * W + x) * 4 + 3] > 24 { minX = min(minX, x); maxX = max(maxX, x); minY = min(minY, y); maxY = max(maxY, y) }
    } }
    guard maxX >= 0 else { print("empty cell \(name)"); continue }
    let bw = maxX - minX + 1, bh = maxY - minY + 1
    // CGImage cropping uses top-left origin in image space
    let crop = cg.cropping(to: CGRect(x: minX, y: minY, width: bw, height: bh))!
    let side = Double(max(bw, bh)) * 1.08
    let scale = Double(size) / side
    let dw = Double(bw) * scale, dh = Double(bh) * scale
    let octx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
                         space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    octx.interpolationQuality = .high
    octx.draw(crop, in: CGRect(x: (Double(size) - dw) / 2, y: (Double(size) - dh) / 2, width: dw, height: dh))
    let rep = NSBitmapImageRep(cgImage: octx.makeImage()!)
    try! rep.representation(using: .png, properties: [:])!.write(to: out.appendingPathComponent(name + ".png"))
}
