from __future__ import annotations
import io, shutil, sys
from pathlib import Path
from PIL import Image, ImageEnhance, ImageOps
from rembg import remove, new_session

def process(src: Path, dst: Path, transparent: bool, color=(255,255,255), optimize=True,
            target_w=0, target_h=0, margin_pct=8, fit_to_canvas=True, object_mode=1, overflow_mode=0, quality_mode=1, mask_cleanup=1) -> None:
    with Image.open(src) as im:
        im = ImageOps.exif_transpose(im).convert("RGB")
        if optimize:
            im = ImageEnhance.Contrast(im).enhance(1.04)
            im = ImageEnhance.Sharpness(im).enhance(1.08)
        b = io.BytesIO()
        im.save(b, "PNG")
        if quality_mode == 0:
            session = new_session("u2netp")
            removed = remove(b.getvalue(), session=session)
        elif quality_mode == 2:
            session = new_session("u2net")
            removed = remove(
                b.getvalue(), session=session, alpha_matting=True,
                alpha_matting_foreground_threshold=240,
                alpha_matting_background_threshold=10,
                alpha_matting_erode_size=10
            )
        else:
            session = new_session("u2net")
            removed = remove(b.getvalue(), session=session)
        fg = Image.open(io.BytesIO(removed)).convert("RGBA")

        if mask_cleanup > 0:
            alpha = fg.getchannel("A")
            if mask_cleanup == 1:
                alpha = alpha.point(lambda a: 0 if a < 28 else (255 if a > 235 else a))
            else:
                alpha = alpha.point(lambda a: 0 if a < 70 else (255 if a > 205 else a))
                from PIL import ImageFilter
                alpha = alpha.filter(ImageFilter.MedianFilter(size=3))
            fg.putalpha(alpha)

        if target_w > 0 and target_h > 0:
            alpha = fg.getchannel("A")
            full_bbox = alpha.getbbox()
            scale_bbox = full_bbox

            if full_bbox and object_mode in (1, 2):
                import numpy as np
                a = np.array(alpha.resize((max(1,alpha.width//4), max(1,alpha.height//4))))
                mask = a > 32
                try:
                    h,w = mask.shape
                    seen = np.zeros_like(mask, dtype=bool)
                    comps=[]
                    for yy in range(h):
                        for xx in range(w):
                            if not mask[yy,xx] or seen[yy,xx]: continue
                            stack=[(xx,yy)]; seen[yy,xx]=True; pts=[]
                            while stack:
                                px,py=stack.pop(); pts.append((px,py))
                                for nx,ny in ((px-1,py),(px+1,py),(px,py-1),(px,py+1)):
                                    if 0<=nx<w and 0<=ny<h and mask[ny,nx] and not seen[ny,nx]:
                                        seen[ny,nx]=True; stack.append((nx,ny))
                            comps.append(pts)
                    if comps:
                        comps.sort(key=len, reverse=True)
                        main=comps[0]
                        xs=[p[0] for p in main]; ys=[p[1] for p in main]
                        focus=(min(xs)*4,min(ys)*4,min(alpha.width,(max(xs)+1)*4),min(alpha.height,(max(ys)+1)*4))
                        if object_mode == 2:
                            keep=np.zeros_like(mask,dtype=np.uint8)
                            for px,py in main: keep[py,px]=255
                            km=Image.fromarray(keep).resize(alpha.size,Image.Resampling.NEAREST)
                            fg.putalpha(Image.composite(alpha,Image.new("L",alpha.size,0),km))
                            alpha=fg.getchannel("A")
                            full_bbox=alpha.getbbox()
                            scale_bbox=full_bbox
                        else:
                            # Main object controls scale, but full foreground is retained.
                            scale_bbox=focus
                except Exception:
                    scale_bbox=full_bbox

            if full_bbox:
                usable_w=max(1,int(target_w*(1-2*margin_pct/100)))
                usable_h=max(1,int(target_h*(1-2*margin_pct/100)))
                sw=max(1,scale_bbox[2]-scale_bbox[0]); sh=max(1,scale_bbox[3]-scale_bbox[1])
                scale=min(usable_w/sw, usable_h/sh) if fit_to_canvas else min(1.0,usable_w/sw,usable_h/sh)

                # First scale according to the main object.
                # Then enforce a second constraint: the COMPLETE foreground must fit
                # inside the target canvas with the selected safety margin.
                full_w=max(1,full_bbox[2]-full_bbox[0])
                full_h=max(1,full_bbox[3]-full_bbox[1])
                full_fit=min(usable_w/full_w, usable_h/full_h)

                # "Main object prioritized" may enlarge the device, but never so far
                # that a cable/accessory is clipped.
                if object_mode == 1:
                    if overflow_mode == 0:
                        # Safety first: every retained pixel must fit.
                        scale=min(scale,full_fit)
                    elif overflow_mode == 1:
                        # Main object large, but the main device itself must ALWAYS remain
                        # completely inside the selected safety margin. Remote cables/accessories
                        # are allowed to extend beyond the canvas.
                        main_w=max(1,scale_bbox[2]-scale_bbox[0])
                        main_h=max(1,scale_bbox[3]-scale_bbox[1])
                        main_fit=min(usable_w/main_w, usable_h/main_h)
                        scale=min(scale,main_fit)
                    else:
                        # Pure main-object framing: clipping is intentionally allowed.
                        pass

                nw=max(1,int(fg.width*scale)); nh=max(1,int(fg.height*scale))
                resized=fg.resize((nw,nh),Image.Resampling.LANCZOS)

                # Center according to selected overflow behavior.
                # In "main object large" mode, the DEVICE bbox is centered, not the complete
                # cable bbox. This prevents a long cable from pushing the device off-canvas.
                center_bbox = scale_bbox if (object_mode == 1 and overflow_mode == 1) else full_bbox
                fb_cx=((center_bbox[0]+center_bbox[2])/2)*scale
                fb_cy=((center_bbox[1]+center_bbox[3])/2)*scale
                paste_x=int(target_w/2-fb_cx)
                paste_y=int(target_h/2-fb_cy)

                canvas=Image.new("RGBA",(target_w,target_h),(0,0,0,0))
                canvas.alpha_composite(resized,(paste_x,paste_y))
                fg=canvas
        if transparent:
            fg.save(dst.with_suffix(".png"), "PNG", optimize=True)
        else:
            bg = Image.new("RGBA", fg.size, (*color,255))
            out = Image.alpha_composite(bg, fg).convert("RGB")
            if src.suffix.lower() in (".jpg",".jpeg"):
                out.save(dst, "JPEG", quality=95, subsampling=0)
            else:
                out.save(dst, "PNG", optimize=True)

if __name__ == "__main__":
    # args: source, destination, transparent(0/1)
    hexcolor = sys.argv[4].lstrip("#") if len(sys.argv) > 4 else "FFFFFF"
    color = tuple(int(hexcolor[i:i+2],16) for i in (0,2,4))
    optimize = (sys.argv[5] != "0") if len(sys.argv) > 5 else True
    target_w = int(sys.argv[6]) if len(sys.argv) > 6 else 0
    target_h = int(sys.argv[7]) if len(sys.argv) > 7 else 0
    margin_pct = int(sys.argv[8]) if len(sys.argv) > 8 else 5
    fit_to_canvas = (sys.argv[9] != "0") if len(sys.argv) > 9 else True
    object_mode = int(sys.argv[10]) if len(sys.argv) > 10 else 1
    overflow_mode = int(sys.argv[11]) if len(sys.argv) > 11 else 0
    quality_mode = int(sys.argv[12]) if len(sys.argv) > 12 else 1
    mask_cleanup = int(sys.argv[13]) if len(sys.argv) > 13 else 1
    process(Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3] == "1", color, optimize,
            target_w, target_h, margin_pct, fit_to_canvas, object_mode, overflow_mode, quality_mode, mask_cleanup)
