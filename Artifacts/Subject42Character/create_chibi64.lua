local art='D:/fork/HelpMePlease/Artifacts/Subject42Character/'
local out='D:/fork/HelpMePlease/Assets/_Project/art/Sprites/Subject42Idle/'
local s=Sprite(64,64,ColorMode.RGB)
local hex={0x182637,0x2C4259,0x4E708C,0x819FB3,0xB4CCD5,0xBA9FAA,0xE2C9C6,0xF5E9DC,0x43E6CB,0xD78F9F}
local col={};for i,h in ipairs(hex) do col[i]=app.pixelColor.rgba((h>>16)&255,(h>>8)&255,h&255,255) end
local im,cel
local function layer(name)
 if cel then cel.image=im end
 local l=not cel and s.layers[1] or s:newLayer();l.name=name
 im=Image(64,64,ColorMode.RGB);cel=s:newCel(l,1,im,Point(0,0))
end
local function r(x,y,w,h,k) for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,col[k]) end end end
local function p(points,k)
 for y=0,63 do for x=0,63 do
  local inside=false;local j=#points
  for i=1,#points do local a,b=points[i],points[j]
   if ((a[2]>y)~=(b[2]>y)) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then inside=not inside end
   j=i
  end
  if inside then im:drawPixel(x,y,col[k]) end
 end end
end
layer('01 Small boots and legs')
r(24,48,7,10,1);r(34,48,7,10,1)
r(25,49,5,5,6);r(26,49,4,4,7);r(35,49,5,5,6);r(35,49,3,4,7)
p({{24,54},{31,54},{31,59},{22,59},{22,57}},1)
p({{34,54},{41,54},{43,57},{43,59},{34,59}},1)
r(24,55,6,3,2);r(23,57,7,1,3);r(25,55,2,1,5)
r(35,55,6,3,2);r(35,57,7,1,3);r(36,55,2,1,5)
layer('02 Jacket sleeves and skirt')
p({{25,32},{39,32},{43,35},{46,42},{45,46},{41,46},{39,41},{39,43},{44,50},{39,52},{34,51},{29,52},{21,50},{24,43},{24,41},{22,46},{18,45},{18,41},{21,35}},1)
p({{23,35},{27,34},{26,39},{23,43},{20,42},{21,38}},3)
p({{23,35},{25,35},{23,39},{21,40}},4)
p({{38,34},{41,35},{44,41},{43,44},{40,41},{38,38}},3)
r(41,37,2,3,4)
p({{20,42},{23,43},{22,45},{20,45},{19,44}},7);r(20,42,1,2,8)
p({{27,33},{37,33},{40,36},{38,42},{27,42},{25,36}},3)
p({{27,35},{31,36},{31,39},{27,38}},4)
p({{33,36},{37,35},{38,38},{34,39}},4)
r(31,36,2,6,2);r(32,37,1,4,5)
p({{28,32},{36,32},{36,34},{32,36},{28,34}},6)
p({{26,33},{28,32},{31,35},{28,36}},5)
p({{36,32},{39,34},{36,36},{33,35}},5)
r(26,41,13,2,2);r(31,41,3,2,5);r(32,41,1,1,9)
p({{26,43},{38,43},{42,49},{38,50},{34,49},{29,50},{23,49}},3)
p({{27,43},{30,43},{28,49},{25,49}},4)
p({{32,43},{34,43},{34,49},{31,49}},4)
p({{37,44},{39,46},{40,49},{37,48}},2)
r(29,48,1,2,1);r(38,45,2,2,2);r(38,45,2,1,5)
layer('03 Oversized head silhouette')
p({{25,4},{37,4},{43,7},{47,12},{48,21},{46,29},{42,33},{36,35},{27,35},{21,32},{17,27},{16,17},{18,10}},1)
p({{25,5},{37,5},{42,8},{46,13},{46,22},{44,29},{39,32},{25,32},{20,28},{18,20},{19,12}},2)
p({{24,14},{39,13},{43,18},{43,27},{39,32},{34,34},{28,33},{23,30},{20,24},{20,19}},6)
p({{25,14},{37,14},{41,18},{42,25},{39,30},{34,32},{28,31},{23,28},{22,23},{22,18}},7)
p({{26,16},{36,15},{39,18},{39,21},{34,22},{27,21},{24,23},{23,21}},8)
r(19,23,3,4,6);r(20,24,2,2,7);r(43,23,2,4,6);r(43,24,1,2,7)
layer('04 Smile eyes brows and blush')
-- Soft brows, large teal eyes, doubled catchlights and restrained blush.
r(24,19,4,1,2);r(24,20,1,1,2);r(35,19,4,1,2);r(38,20,1,1,2)
p({{24,21},{28,21},{30,23},{30,27},{28,29},{24,28},{23,25},{23,22}},1)
p({{35,21},{39,21},{41,23},{40,27},{38,29},{35,28},{34,25},{34,23}},1)
r(22,22,2,1,1);r(40,22,2,1,1)
r(24,23,5,4,8);r(35,23,5,4,8)
r(26,23,3,4,3);r(36,23,3,4,3)
r(26,25,3,2,9);r(36,25,3,2,9)
r(26,23,2,2,1);r(36,23,2,2,1)
r(25,22,2,2,8);r(35,22,2,2,8);r(28,26,1,1,8);r(38,26,1,1,8)
r(22,28,3,1,10);r(39,28,3,1,10)
r(31,27,1,1,6)
r(30,30,1,1,6);r(31,31,3,1,6);r(34,30,1,1,6)
r(31,30,3,1,8)
layer('05 Side swept hair and lab clip')
p({{25,5},{36,5},{42,8},{46,13},{46,21},{44,24},{42,22},{41,17},{37,14},{33,16},{29,17},{26,20},{22,22},{21,28},{19,27},{18,21},{18,15},{20,10}},2)
p({{25,6},{35,6},{40,8},{43,12},{37,10},{31,11},{27,15},{23,18},{20,20},{20,15},{22,10}},3)
p({{26,7},{34,7},{38,9},{32,9},{28,11},{25,14},{22,15},{23,11}},4)
p({{27,7},{32,7},{31,8},{27,9},{25,11},{24,11},{25,9}},5)
p({{33,11},{38,11},{41,14},{38,13},{35,15},{29,16}},3)
p({{43,14},{45,16},{45,23},{43,27},{42,29},{42,24}},3)
r(43,18,1,5,4)
r(40,16,4,2,1);r(41,16,3,1,9)
layer('06 Forearm implant')
p({{41,41},{46,41},{46,45},{44,47},{41,46}},1)
r(42,42,3,3,9);r(42,42,1,2,8);r(43,45,2,1,6)
cel.image=im
local pal=Palette(11);pal:setColor(0,Color{r=0,g=0,b=0,a=0});for i,v in ipairs(col) do pal:setColor(i,Color{rgbaPixel=v}) end;s:setPalette(pal)
local tag=s:newTag(1,1);tag.name='Idle_Chibi'
s:saveAs(art..'Subject42_Female64_Chibi.aseprite')
s:saveCopyAs(out..'Subject42_Female64_Chibi.png')
local check=app.open(out..'Subject42_Female64_Chibi.png');assert(check.width==64 and check.height==64 and #check.frames==1)
local seen={};local n,count=0,0
for pixel in check.cels[1].image:pixels() do local v=pixel();local a=app.pixelColor.rgbaA(v);assert(a==0 or a==255);if a==255 then count=count+1;if not seen[v] then seen[v]=true;n=n+1 end end end
assert(n==10 and count>1000)
local f=io.open(art..'Female64_Chibi_validation.txt','w');f:write('64x64; 1 static frame; '..n..' opaque colors; '..count..' solid pixels; binary alpha; 6 editable layers.\n');f:close()
check:resize(512,512);check:saveCopyAs(art..'Female64_Chibi_Preview.png')
