local art='D:/fork/HelpMePlease/Artifacts/Subject42Character/'
local out='D:/fork/HelpMePlease/Assets/_Project/art/Sprites/Subject42Idle/'
local s=Sprite(64,64,ColorMode.RGB)
local hex={0x162330,0x2C4053,0x506B83,0x829CAC,0xBCD0D5,0x887F8F,0xB8ACB5,0xE0D2CF,0x37E3CB,0xDAFFF1}
local colors={}
for i,h in ipairs(hex) do colors[i]=app.pixelColor.rgba((h>>16)&255,(h>>8)&255,h&255,255) end
local im,activeCel
local function layer(name)
 if activeCel then activeCel.image=im end
 local l
 if #s.layers==1 and not s.layers[1]:cel(1) then l=s.layers[1] else l=s:newLayer() end
 l.name=name; im=Image(64,64,ColorMode.RGB); activeCel=s:newCel(l,1,im,Point(0,0))
end
local function rect(x,y,w,h,k)
 for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,colors[k]) end end
end
local function poly(points,k)
 for y=0,63 do for x=0,63 do
  local inside=false; local j=#points
  for i=1,#points do
   local a,b=points[i],points[j]
   if ((a[2]>y)~=(b[2]>y)) and (x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]) then inside=not inside end
   j=i
  end
  if inside then im:drawPixel(x,y,colors[k]) end
 end end
end
layer('01 Legs and worn boots')
poly({{23,40},{32,40},{31,54},{30,58},{21,58},{21,55},{23,53}},1)
poly({{34,40},{42,40},{41,53},{44,55},{44,58},{34,58},{33,53}},1)
poly({{24,42},{30,42},{29,51},{24,51}},6)
rect(25,43,4,6,7); rect(25,43,2,4,8)
poly({{35,42},{40,42},{39,51},{35,51}},6)
rect(35,43,3,6,7)
poly({{24,50},{30,50},{29,55},{28,56},{22,56},{23,54}},2)
rect(24,51,2,3,4);rect(26,51,3,1,3);rect(22,56,7,1,3)
poly({{34,50},{40,50},{40,54},{43,55},{43,56},{35,56}},2)
rect(35,51,2,3,4);rect(37,51,3,1,3);rect(35,56,8,1,3)
layer('02 Fitted lab jacket and arms')
poly({{26,20},{38,20},{43,22},{46,27},{46,36},{43,39},{40,37},{39,29},{38,32},{26,32},{24,29},{23,36},{21,38},{18,36},{19,27},{22,22}},1)
poly({{23,23},{27,22},{27,27},{24,29},{22,34},{19,33},{20,27}},3)
poly({{23,23},{26,23},{24,27},{21,30},{21,27}},4)
poly({{39,22},{42,23},{45,28},{45,31},{41,32},{40,27},{38,26}},3)
rect(42,26,2,4,4)
poly({{20,33},{23,34},{22,36},{20,37},{19,35}},7)
rect(20,34,1,2,8)
-- Broad, curved clothed bust narrows into the waist.
poly({{27,21},{37,21},{40,25},{40,28},{37,31},{37,34},{27,34},{27,31},{24,28},{24,25}},3)
poly({{27,23},{31,24},{31,28},{28,29},{25,27},{25,25}},4)
poly({{33,24},{37,23},{39,25},{39,27},{36,29},{33,28}},4)
rect(27,24,3,1,5);rect(34,24,3,1,5)
poly({{25,28},{29,30},{31,29},{32,30},{34,29},{38,28},{36,32},{28,32}},2)
rect(28,31,8,3,3);rect(29,31,2,2,4)
rect(31,25,2,8,2);rect(32,26,1,6,4)
-- High open collar shows only neck; torso remains covered.
poly({{28,20},{36,20},{36,23},{32,25},{28,23}},6)
poly({{29,20},{35,20},{35,22},{32,23},{29,22}},7)
poly({{26,21},{28,20},{31,24},{28,24}},5)
poly({{36,20},{39,22},{36,24},{33,24}},4)
layer('03 Belt and torn pleated skirt')
poly({{27,32},{38,32},{40,36},{44,43},{40,45},{36,44},{33,45},{29,44},{25,45},{20,43},{24,36}},1)
poly({{27,34},{37,34},{40,38},{42,42},{39,43},{35,42},{32,43},{28,42},{25,43},{22,42},{25,37}},3)
poly({{27,35},{29,35},{27,41},{24,42}},4)
poly({{31,35},{33,35},{33,42},{29,42}},4)
poly({{36,35},{37,35},{41,42},{37,41}},2)
poly({{27,37},{28,37},{26,43},{25,43}},2)
rect(33,36,1,6,2);rect(28,42,2,1,1)
rect(26,32,13,2,2);rect(31,32,3,2,5);rect(32,32,1,1,3)
rect(38,36,3,3,2);rect(39,36,2,1,4)
layer('04 Adult face and cropped hair')
poly({{27,5},{36,5},{40,8},{41,12},{40,19},{37,22},{35,21},{28,21},{24,19},{23,12},{24,8}},1)
poly({{27,9},{36,9},{38,12},{38,18},{35,21},{30,21},{26,18},{25,13}},6)
poly({{28,10},{36,10},{37,13},{36,18},{34,20},{30,19},{27,17},{27,13}},7)
poly({{29,11},{35,11},{35,14},{32,15},{29,14}},8)
rect(27,15,3,1,1);rect(34,15,3,1,1)
rect(28,16,1,1,5);rect(35,16,1,1,5)
rect(32,16,1,2,6);rect(31,19,3,1,6)
poly({{27,6},{35,6},{39,9},{39,13},{37,14},{35,11},{31,11},{28,13},{26,16},{25,19},{24,14},{24,10}},2)
poly({{27,7},{34,7},{37,9},{32,9},{28,11},{26,14},{25,13},{26,9}},3)
poly({{28,7},{33,7},{32,8},{28,9},{27,10},{26,10}},4)
poly({{38,11},{40,12},{39,19},{37,20},{38,16}},2)
rect(38,13,1,4,3)
layer('05 Forearm anomaly and subject marker')
poly({{41,31},{46,31},{46,36},{44,39},{41,37}},1)
rect(42,32,3,4,9);rect(42,32,1,3,10)
rect(42,37,2,1,7);rect(43,36,2,1,2)
rect(25,26,1,2,9)
activeCel.image=im
local pal=Palette(11);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,v in ipairs(colors) do pal:setColor(i,Color{rgbaPixel=v}) end
s:setPalette(pal)
local tag=s:newTag(1,1);tag.name='Idle_Down'
s:saveAs(art..'Subject42_Female64_Idle.aseprite')
s:saveCopyAs(out..'Subject42_Female64_Idle.png')
local check=app.open(out..'Subject42_Female64_Idle.png')
local seen={};local n=0
for p in check.cels[1].image:pixels() do
 local v=p();local a=app.pixelColor.rgbaA(v);assert(a==0 or a==255)
 if a==255 and not seen[v] then seen[v]=true;n=n+1 end
end
assert(n>=6 and n<=10 and check.width==64 and check.height==64)
local f=io.open(art..'Female64_validation.txt','w');f:write('64x64; '..n..' opaque colours; alpha 0/255; 1 idle frame; '..#s.layers..' editable layers.\n');f:close()
check:resize(512,512);check:saveCopyAs(art..'Female64_Preview.png')
