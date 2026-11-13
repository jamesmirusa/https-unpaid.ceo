// Soft drop shadow for UI panels as a 9-slice PNG (black, alpha only): a rounded rect blurred, centre left empty
// enough to stretch. usage: swift make_shadow.swift <out.png>   (192×192, radius 36, blur 28, slices 72)
import AppKit
let size = 192, inset = 56.0, radius = 36.0, blur = 28.0
let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
                    space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let rect = CGRect(x: inset, y: inset, width: Double(size) - 2 * inset, height: Double(size) - 2 * inset)
// draw the shape far off-canvas and keep only its shadow
let off = 1000.0
ctx.setShadow(offset: CGSize(width: off, height: 0), blur: blur, color: CGColor(gray: 0, alpha: 0.55))
ctx.addPath(CGPath(roundedRect: rect.offsetBy(dx: -off, dy: 0), cornerWidth: radius, cornerHeight: radius, transform: nil))
ctx.setFillColor(CGColor(gray: 0, alpha: 1))
ctx.fillPath()
let rep = NSBitmapImageRep(cgImage: ctx.makeImage()!)
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
