local art='D:/fork/HelpMePlease/Artifacts/Subject42SlotMachine/'
local out='D:/fork/HelpMePlease/Assets/_Project/art/Sprites/Subject42SlotMachine/'
local s=Sprite(32,32,ColorMode.RGB)
local hex={0x14232F,0x293E50,0x48667D,0x7894A4,0xB9CDD1,0xE0E6DB,0x248C91,0x36E4C5,0xD3FFF0}
local col={};for i,h in ipairs(hex) do col[i]=app.pixelColor.rgba((h>>16)&255,(h>>8)&255,h&255,255) end
local im,cel
local function layer(name)
 if cel then cel.image=im end
 local l=not cel and s.layers[1] or s:newLayer();l.name=name
 im=Image(32,32,ColorMode.RGB);cel=s:newCel(l,1,im,Point(0,0))
end
local function r(x,y,w,h,k) for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,col[k]) end end end
layer('01 Cabinet')
-- Raised top plane, recessed face and narrow right side.
r(6,2,16,1,1);r(4,3,20,1,1);r(3,4,22,24,1)
r(4,4,19,22,3);r(23,5,1,21,2)
r(6,3,16,1,4);r(4,4,19,2,4);r(6,4,16,1,5)
r(4,6,1,19,4);r(22,6,1,20,2)
r(3,27,22,3,1);r(4,27,20,1,4);r(4,28,20,1,2)
r(5,30,4,1,1);r(20,30,4,1,1)
-- Worn paint, restrained to cabinet corners.
r(5,7,1,2,5);r(21,24,1,2,4);r(5,25,2,1,2)
layer('02 Header and three reels')
r(6,6,15,5,1);r(7,7,13,3,2)
r(8,8,3,1,8);r(12,8,3,1,8);r(16,8,3,1,8)
r(5,12,17,9,1)
for _,x in ipairs({6,11,16}) do
 r(x,13,5,7,2);r(x,13,4,6,5);r(x,14,4,4,6)
end
-- Pixel symbols: seven, anomalous core, seven.
r(6,14,3,1,7);r(8,15,1,1,7);r(7,16,1,2,7)
r(12,14,2,1,7);r(11,15,1,2,7);r(14,15,1,2,7);r(12,17,2,1,7);r(12,15,2,2,8)
r(16,14,3,1,7);r(18,15,1,1,7);r(17,16,1,2,7)
layer('03 Controls payout and lever')
r(6,22,15,1,4);r(6,23,15,3,2)
r(7,23,6,2,1);r(8,23,4,1,4)
r(16,23,4,2,1);r(16,23,3,1,8);r(17,23,1,1,9)
r(10,26,7,1,1);r(11,26,5,1,2)
r(25,17,3,3,1);r(25,18,2,1,4)
r(27,10,2,9,1);r(27,11,1,7,5)
r(26,7,3,1,1);r(25,8,5,3,1);r(26,11,3,1,1)
r(26,8,3,3,7);r(26,8,2,2,8);r(26,8,1,1,9)
cel.image=im
local pal=Palette(10);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,v in ipairs(col) do pal:setColor(i,Color{rgbaPixel=v}) end;s:setPalette(pal)
s:saveAs(art..'Subject42_SlotMachine_32.aseprite')
s:saveCopyAs(out..'Subject42_SlotMachine_32.png')
local check=app.open(out..'Subject42_SlotMachine_32.png')
assert(check.width==32 and check.height==32 and #check.frames==1)
local seen={};local n=0;local count=0
for p in check.cels[1].image:pixels() do local v=p();local a=app.pixelColor.rgbaA(v);assert(a==0 or a==255);if a==255 then count=count+1;if not seen[v] then seen[v]=true;n=n+1 end end end
assert(n>=6 and n<=10 and count>0)
local f=io.open(art..'validation.txt','w');f:write('32x32; '..n..' opaque colors; alpha 0/255 only; '..count..' opaque pixels; 1 frame; 3 editable layers.\n');f:close()
check:resize(384,384);check:saveCopyAs(art..'SlotMachine_Preview.png')
