"""
Draws the default avatars of the apps (flat illustrations, 512x512 PNG) with Pillow.

    python tools/generate_avatars.py

Writes apps/packages/ottoman_core/assets/avatars/{male_1..3,female_1..3,neutral}.png. Everything is drawn at 4x and
scaled down for smooth edges. Change a colour or style here and run the script again; nothing is downloaded.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

SIZE = 512
SS = 4  # supersampling factor
OUT = os.path.join(os.path.dirname(__file__), '..', 'apps', 'packages', 'ottoman_core', 'assets', 'avatars')


def shade(color, f):
    """Darker (f<1) or lighter (f>1) version of an RGB colour."""
    return tuple(max(0, min(255, int(c * f))) for c in color)


class Canvas:
    def __init__(self, bg):
        self.img = Image.new('RGBA', (SIZE * SS, SIZE * SS), bg)
        self.d = ImageDraw.Draw(self.img, 'RGBA')

    def s(self, v):
        return int(round(v * SS))

    def box(self, cx, cy, rx, ry):
        return [self.s(cx - rx), self.s(cy - ry), self.s(cx + rx), self.s(cy + ry)]

    def ellipse(self, cx, cy, rx, ry, fill, outline=None, width=0):
        self.d.ellipse(self.box(cx, cy, rx, ry), fill=fill, outline=outline, width=self.s(width) if width else 0)

    def poly(self, pts, fill):
        self.d.polygon([(self.s(x), self.s(y)) for x, y in pts], fill=fill)

    def rrect(self, x0, y0, x1, y1, r, fill):
        self.d.rounded_rectangle([self.s(x0), self.s(y0), self.s(x1), self.s(y1)], radius=self.s(r), fill=fill)

    def line(self, pts, fill, width):
        self.d.line([(self.s(x), self.s(y)) for x, y in pts], fill=fill, width=self.s(width), joint='curve')

    def arc(self, cx, cy, rx, ry, a0, a1, fill, width):
        self.d.arc(self.box(cx, cy, rx, ry), a0, a1, fill=fill, width=self.s(width))

    def pie(self, cx, cy, rx, ry, a0, a1, fill):
        self.d.pieslice(self.box(cx, cy, rx, ry), a0, a1, fill=fill)

    def chord(self, cx, cy, rx, ry, a0, a1, fill):
        self.d.chord(self.box(cx, cy, rx, ry), a0, a1, fill=fill)

    def save(self, name):
        out = self.img.resize((SIZE, SIZE), Image.LANCZOS).convert('RGB')
        os.makedirs(OUT, exist_ok=True)
        out.save(os.path.join(OUT, name), optimize=True)
        return out


HEAD_X, HEAD_Y = 256, 232


def torso(c, shirt, skin, collar=None, v_neck=True):
    c.ellipse(256, 575, 215, 170, shirt)
    # soft shoulder shading
    c.ellipse(256, 590, 215, 170, shade(shirt, 0.9))
    c.ellipse(256, 575, 215, 170, shirt)
    c.rrect(222, 300, 290, 410, 26, skin)
    c.rrect(222, 300, 290, 345, 20, shade(skin, 0.86))  # shadow under the chin
    if v_neck:
        c.poly([(222, 392), (290, 392), (256, 455)], skin)
        if collar:
            c.poly([(204, 380), (232, 372), (256, 452), (236, 462)], collar)
            c.poly([(308, 380), (280, 372), (256, 452), (276, 462)], collar)
    else:
        c.ellipse(256, 398, 44, 22, skin)


def head(c, skin, jaw_square=0.0, cheeks=True):
    # ears first, the head covers their inner half
    for sx in (-1, 1):
        c.ellipse(HEAD_X + sx * 90, HEAD_Y + 18, 17, 26, shade(skin, 0.97))
        c.ellipse(HEAD_X + sx * 92, HEAD_Y + 20, 8, 14, shade(skin, 0.84))
    c.ellipse(HEAD_X, HEAD_Y, 90, 104, skin)
    if jaw_square:
        c.rrect(HEAD_X - 86, HEAD_Y + 10, HEAD_X + 86, HEAD_Y + 106, 54 - 20 * jaw_square, skin)
    if cheeks:
        for sx in (-1, 1):
            c.ellipse(HEAD_X + sx * 52, HEAD_Y + 52, 22, 13, shade((235, 120, 110), 1.0) + (70,))


def hair_cap(c, hair, skin, shift=0, rise=46, wide=74):
    """Hair on the top of the head with a curved hairline: the cap is cut by a forehead-shaped ellipse of skin."""
    c.pie(HEAD_X, HEAD_Y - 14, 98, 106, 180, 360, hair)
    c.ellipse(HEAD_X + shift, HEAD_Y - 20, wide, rise + 6, skin)


def face(c, skin, eye=(52, 38, 32), brow=(60, 44, 36), smile=True, lashes=False, brow_w=7, brow_dy=0):
    for sx in (-1, 1):
        ex = HEAD_X + sx * 38
        c.ellipse(ex, HEAD_Y + 14, 14, 10, (255, 255, 255))
        c.ellipse(ex, HEAD_Y + 15, 8.5, 8.5, eye)
        c.ellipse(ex + 2.5, HEAD_Y + 12, 2.6, 2.6, (255, 255, 255))
        if lashes:
            c.arc(ex, HEAD_Y + 14, 15, 11, 200, 340, (40, 30, 28), 3.2)
        y = HEAD_Y - 14 + brow_dy
        c.line([(ex - 17, y + 2 * sx * -1 + 3), (ex, y - 4), (ex + 17, y + 2 * sx + 3)], brow, brow_w)
    # nose
    c.arc(HEAD_X, HEAD_Y + 48, 13, 13, 20, 160, shade(skin, 0.72), 4)
    # mouth
    if smile:
        c.arc(HEAD_X, HEAD_Y + 66, 28, 22, 20, 160, (150, 62, 60), 5)
    else:
        c.line([(HEAD_X - 20, HEAD_Y + 78), (HEAD_X + 20, HEAD_Y + 78)], (150, 62, 60), 5)


def glasses(c, color=(35, 38, 45)):
    for sx in (-1, 1):
        c.arc(HEAD_X + sx * 38, HEAD_Y + 14, 30, 24, 0, 360, color, 5)
    c.line([(HEAD_X - 9, HEAD_Y + 12), (HEAD_X + 9, HEAD_Y + 12)], color, 5)
    for sx in (-1, 1):
        c.line([(HEAD_X + sx * 68, HEAD_Y + 10), (HEAD_X + sx * 90, HEAD_Y + 4)], color, 5)


# ------------------------------------------------------------------------------------------------- male


def male_1():
    """Short textured hair, clean shaven, blue shirt."""
    skin, hair = (240, 200, 168), (62, 42, 30)
    c = Canvas((255, 226, 180))
    torso(c, (52, 107, 168), skin, collar=(40, 88, 140))
    head(c, skin, jaw_square=0.6)
    hair_cap(c, hair, skin, shift=-18, rise=44, wide=70)
    c.ellipse(HEAD_X + 14, HEAD_Y - 116, 76, 30, hair)  # the quiff
    c.poly([(HEAD_X - 20, HEAD_Y - 100), (HEAD_X + 70, HEAD_Y - 82), (HEAD_X + 40, HEAD_Y - 52), (HEAD_X - 40, HEAD_Y - 78)], hair)
    face(c, skin)
    return c.save('male_1.png')


def male_2():
    """Short hair, full beard and moustache, black barber shirt."""
    skin, hair = (214, 160, 120), (34, 26, 22)
    c = Canvas((176, 222, 214))
    torso(c, (38, 42, 50), skin, collar=(60, 66, 76))
    head(c, skin, jaw_square=0.8)
    # beard: lower half of the face, the mouth is cut out of it afterwards
    c.chord(HEAD_X, HEAD_Y + 6, 92, 108, 10, 170, hair)
    for sx in (-1, 1):
        c.poly([(HEAD_X + sx * 92, HEAD_Y + 12), (HEAD_X + sx * 80, HEAD_Y - 18), (HEAD_X + sx * 70, HEAD_Y + 22)], hair)
    c.ellipse(HEAD_X, HEAD_Y + 62, 36, 22, shade(skin, 1.02))  # mouth area
    hair_cap(c, hair, skin, rise=36, wide=72)
    c.ellipse(HEAD_X, HEAD_Y - 108, 80, 24, hair)
    face(c, skin, brow=(34, 26, 22), brow_w=8)
    # moustache over the mouth
    c.poly([(HEAD_X - 38, HEAD_Y + 56), (HEAD_X, HEAD_Y + 44), (HEAD_X + 38, HEAD_Y + 56), (HEAD_X + 30, HEAD_Y + 66),
            (HEAD_X, HEAD_Y + 58), (HEAD_X - 30, HEAD_Y + 66)], hair)
    c.arc(HEAD_X, HEAD_Y + 72, 20, 14, 20, 160, (176, 84, 80), 5)
    return c.save('male_2.png')


def male_3():
    """Slicked-back hair with a side part, glasses, light stubble, olive shirt."""
    skin, hair = (250, 214, 186), (146, 100, 58)
    c = Canvas((233, 214, 245))
    torso(c, (92, 112, 78), skin, collar=(72, 90, 60))
    head(c, skin, jaw_square=0.4)
    c.chord(HEAD_X, HEAD_Y + 6, 90, 104, 15, 165, shade(hair, 0.92) + (38,))  # stubble
    c.ellipse(HEAD_X, HEAD_Y + 64, 34, 20, skin)
    hair_cap(c, hair, skin, shift=-8, rise=48, wide=72)
    c.ellipse(HEAD_X + 8, HEAD_Y - 112, 80, 30, hair)
    for i in range(5):  # combed strands
        x = HEAD_X - 60 + i * 26
        c.line([(x, HEAD_Y - 100), (x + 24, HEAD_Y - 56)], shade(hair, 0.78), 4)
    c.line([(HEAD_X - 34, HEAD_Y - 112), (HEAD_X - 40, HEAD_Y - 70)], shade(hair, 0.7), 5)  # the parting
    face(c, skin, brow=shade(hair, 0.7), brow_w=7)
    glasses(c)
    return c.save('male_3.png')


# ----------------------------------------------------------------------------------------------- female


def female_1():
    """Long straight hair, pink top."""
    skin, hair = (243, 205, 178), (92, 54, 34)
    c = Canvas((222, 214, 247))
    c.rrect(HEAD_X - 118, HEAD_Y - 100, HEAD_X + 118, 600, 100, hair)  # hair behind the shoulders
    torso(c, (226, 120, 150), skin, collar=(200, 96, 128), v_neck=False)
    c.rrect(HEAD_X - 118, HEAD_Y + 40, HEAD_X - 74, 470, 40, hair)  # locks in front of the shoulders
    c.rrect(HEAD_X + 74, HEAD_Y + 40, HEAD_X + 118, 470, 40, hair)
    head(c, skin)
    hair_cap(c, hair, skin, rise=50, wide=70)
    c.poly([(HEAD_X - 98, HEAD_Y - 16), (HEAD_X - 96, HEAD_Y + 60), (HEAD_X - 76, HEAD_Y + 20), (HEAD_X - 62, HEAD_Y - 40)], hair)
    c.poly([(HEAD_X + 98, HEAD_Y - 16), (HEAD_X + 96, HEAD_Y + 60), (HEAD_X + 76, HEAD_Y + 20), (HEAD_X + 62, HEAD_Y - 40)], hair)
    face(c, skin, lashes=True, brow=shade(hair, 0.8), brow_w=6)
    return c.save('female_1.png')


def female_2():
    """Hair in a bun, earrings, teal top."""
    skin, hair = (224, 172, 138), (30, 24, 24)
    c = Canvas((255, 214, 196))
    torso(c, (46, 139, 134), skin, collar=(36, 112, 108), v_neck=False)
    c.ellipse(HEAD_X, HEAD_Y - 128, 46, 42, hair)  # the bun
    c.ellipse(HEAD_X, HEAD_Y - 128, 46, 42, hair)
    head(c, skin)
    hair_cap(c, hair, skin, rise=52, wide=72)
    c.poly([(HEAD_X - 96, HEAD_Y - 12), (HEAD_X - 98, HEAD_Y + 26), (HEAD_X - 80, HEAD_Y - 6)], hair)
    c.poly([(HEAD_X + 96, HEAD_Y - 12), (HEAD_X + 98, HEAD_Y + 26), (HEAD_X + 80, HEAD_Y - 6)], hair)
    for sx in (-1, 1):
        c.ellipse(HEAD_X + sx * 91, HEAD_Y + 52, 8, 8, (240, 196, 80))  # earrings
        c.ellipse(HEAD_X + sx * 91, HEAD_Y + 68, 6, 9, (240, 196, 80))
    face(c, skin, lashes=True, brow=(30, 24, 24), brow_w=6)
    return c.save('female_2.png')


def female_3():
    """Chin-length bob with a fringe, mustard top."""
    skin, hair = (253, 222, 200), (178, 84, 44)
    c = Canvas((200, 232, 214))
    c.rrect(HEAD_X - 112, HEAD_Y - 96, HEAD_X + 112, HEAD_Y + 112, 60, hair)  # the back of the bob
    torso(c, (232, 178, 56), skin, collar=(205, 152, 40), v_neck=False)
    head(c, skin)
    c.pie(HEAD_X, HEAD_Y - 16, 100, 110, 180, 360, hair)
    c.poly([(HEAD_X - 100, HEAD_Y - 16), (HEAD_X - 108, HEAD_Y + 112), (HEAD_X - 74, HEAD_Y + 100), (HEAD_X - 70, HEAD_Y - 6)], hair)
    c.poly([(HEAD_X + 100, HEAD_Y - 16), (HEAD_X + 108, HEAD_Y + 112), (HEAD_X + 74, HEAD_Y + 100), (HEAD_X + 70, HEAD_Y - 6)], hair)
    c.poly([(HEAD_X - 94, HEAD_Y - 30), (HEAD_X - 96, HEAD_Y - 40), (HEAD_X + 96, HEAD_Y - 40), (HEAD_X + 94, HEAD_Y - 30),
            (HEAD_X + 40, HEAD_Y - 6), (HEAD_X - 6, HEAD_Y - 22), (HEAD_X - 50, HEAD_Y - 4)], hair)  # the fringe
    face(c, skin, lashes=True, brow=shade(hair, 0.7), brow_w=6, brow_dy=6)
    return c.save('female_3.png')


# ------------------------------------------------------------------------------------------------ neutral


def neutral():
    """A quiet silhouette for accounts that are not a barber (admin) or have no gender set."""
    c = Canvas((225, 230, 236))
    c.ellipse(256, 590, 215, 175, (140, 150, 163))
    c.rrect(226, 300, 286, 410, 26, (122, 133, 147))
    c.ellipse(HEAD_X, HEAD_Y, 88, 102, (160, 170, 182))
    return c.save('neutral.png')


def contact_sheet(images):
    sheet = Image.new('RGB', (SIZE * 4 // 2, SIZE * 2 // 2), (255, 255, 255))
    for i, im in enumerate(images):
        t = im.resize((SIZE // 2, SIZE // 2), Image.LANCZOS)
        sheet.paste(t, ((i % 4) * SIZE // 2, (i // 4) * SIZE // 2))
    return sheet


if __name__ == '__main__':
    made = [male_1(), male_2(), male_3(), neutral(), female_1(), female_2(), female_3()]
    sheet = contact_sheet(made)
    preview = os.environ.get('AVATAR_PREVIEW')
    if preview:
        sheet.save(preview)
    print('written to', os.path.abspath(OUT))
