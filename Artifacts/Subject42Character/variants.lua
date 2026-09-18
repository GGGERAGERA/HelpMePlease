local art='D:/fork/HelpMePlease/Artifacts/Subject42Character/'
local out='D:/fork/HelpMePlease/Assets/_Project/art/Sprites/Subject42Idle/'
local function c(h) return app.pixelColor.rgba((h>>16)&255,(h>>8)&255,h&255,255) end
local base=app.open(art..'Subject42_Idle_Base.aseprite')
local sheet=Sprite(144,40,ColorMode.RGB)
sheet.layers[1].name='01 Base - cyan'
local function flattened(s)
 local im=Image(32,32,ColorMode.RGB)
 im:drawSprite(s,1)
 return im
end
sheet:newCel(sheet.layers[1],1,flattened(base),Point(4,4))
local specs={
 {'Hood_Cyan',{},true},
 {'Shaved_Amber',{[c(0x394A5A)]=c(0x90A9B6),[c(0x32E6C3)]=c(0xFFB84D),[c(0xE1FFF1)]=c(0xFFF0C9)},false},
 {'Lab_Violet',{[c(0x526E85)]=c(0x90A9B6),[c(0x90A9B6)]=c(0xBCC8CB),[c(0x32E6C3)]=c(0xC881FF),[c(0xE1FFF1)]=c(0xF2DDFF)},false}
}
for i,spec in ipairs(specs) do
 local s=Sprite(base)
 for _,cel in ipairs(s.cels) do
  local im=cel.image:clone()
  for p in im:pixels() do if spec[2][p()] then p(spec[2][p()]) end end
  cel.image=im
 end
 if spec[3] then
  local im=s.layers[1]:cel(1).image:clone()
  for y=3,7 do
   for x=11,20 do
    local pix=im:getPixel(x,y)
    if pix==c(0x394A5A) or pix==c(0x90A9B6) then
      im:drawPixel(x,y,c(y<5 and 0x90A9B6 or 0x526E85))
    end
   end
  end
  im:drawPixel(11,8,c(0x526E85)); im:drawPixel(20,8,c(0x526E85))
  s.layers[1]:cel(1).image=im
 end
 s:saveAs(art..'Subject42_Idle_'..spec[1]..'.aseprite')
 s:saveCopyAs(out..'Subject42_Idle_'..spec[1]..'.png')
 local layer=sheet:newLayer(); layer.name=string.format('%02d ',i+1)..spec[1]
 sheet:newCel(layer,1,flattened(s),Point(4+i*36,4))
 s:close()
end
sheet:saveAs(art..'Subject42_Idle_Comparison.aseprite')
sheet:resize(1152,320)
sheet:saveCopyAs(art..'Comparison_Preview.png')
-- Validate actual export pixels and reopened native files.
local names={'Base','Hood_Cyan','Shaved_Amber','Lab_Violet'}
local report=io.open(art..'validation.txt','w')
for _,name in ipairs(names) do
 local s=app.open(out..'Subject42_Idle_'..name..'.png')
 assert(s.width==32 and s.height==32 and #s.frames==1)
 local colors={}; local n=0; local opaque=0
 for p in s.cels[1].image:pixels() do
  local v=p(); local a=app.pixelColor.rgbaA(v)
  assert(a==0 or a==255,'Intermediate alpha')
  if a==255 then opaque=opaque+1; if not colors[v] then colors[v]=true; n=n+1 end end
 end
 assert(n<=10)
 local src=app.open(art..'Subject42_Idle_'..name..'.aseprite')
 assert(#src.layers==2 and #src.frames==1)
 report:write(name..': 32x32, '..n..' opaque colors, alpha 0/255 only, '..opaque..' solid pixels, 2 editable layers, 1 frame\n')
 src:close(); s:close()
end
report:close()
