local out = 'D:/fork/HelpMePlease/Assets/_Project/art/Sprites/Subject42Idle/'
local art = 'D:/fork/HelpMePlease/Artifacts/Subject42Character/'
local rows = {
'.......OOOOOO.......',
'......OHHHHHHO......',
'.....OHHHLLHHHO.....',
'.....OHLLLLLHHHO....',
'.....OHLLLLLLHHO....',
'.....OSLLLLLSSHO....',
'.....OSLLSLLSSO.....',
'......OSOSLOSO......',
'......OSSSSSSO......',
'.....OOOSSSOOOO.....',
'....OBBBOOOBBBBO....',
'...OBLBBBBBBBLBBO...',
'...OBLBBDDBBBLBBO...',
'...OBBBOBDDBOBBBO...',
'...OBBBOBDDBOBBBO...',
'...OBBBOBDDBODDDO...',
'...OSSSOBDDDOAXAO...',
'...OSLSOBBBBOAXAO...',
'....OSOOBBBBOAXAO...',
'.......ODDDDOODO....',
'......OBBBOBBBO.....',
'......OBBBOBBBO.....',
'......OBLBOBLBO.....',
'......OBBBOBBBO.....',
'......ODDO.ODDO.....',
'.....ODDDO.ODDDO....',
'.....OOOOO.OOOOO....',
}
local hex = {O=0x17232F,D=0x293D50,B=0x526E85,L=0x90A9B6,H=0x394A5A,S=0xBCC8CB,A=0x32E6C3,X=0xE1FFF1}
local function color(n) return app.pixelColor.rgba((n>>16)&255,(n>>8)&255,n&255,255) end
local s=Sprite(32,32,ColorMode.RGB)
s.layers[1].name='01 Body'
local body=Image(32,32,ColorMode.RGB)
local implant=Image(32,32,ColorMode.RGB)
for y,row in ipairs(rows) do
  assert(#row==20,'Row '..y..' width '..#row)
  for x=1,#row do
    local k=row:sub(x,x)
    if hex[k] then
      local im=(k=='A' or k=='X') and implant or body
      im:drawPixel(x+5,y+1,color(hex[k]))
    end
  end
end
-- Two intentional worn seams, with no loose noise outside silhouette.
body:drawPixel(14,15,color(hex.L))
body:drawPixel(18,24,color(hex.D))
s:newCel(s.layers[1],1,body,Point(0,0))
local layer=s:newLayer(); layer.name='02 Anomaly - forearm'
s:newCel(layer,1,implant,Point(0,0))
local pal=Palette(9); pal:setColor(0,Color{r=0,g=0,b=0,a=0})
local keys={'O','D','B','L','H','S','A','X'}
for i,k in ipairs(keys) do pal:setColor(i,Color{rgbaPixel=color(hex[k])}) end
s:setPalette(pal)
local tag=s:newTag(1,1); tag.name='Idle_Down'
s:saveAs(art..'Subject42_Idle_Base.aseprite')
s:saveCopyAs(out..'Subject42_Idle_Base.png')
local preview=Sprite(s); preview:resize(384,384)
preview:saveCopyAs(art..'Idle_Base_Preview.png'); preview:close()
print('Saved base: 32x32; 8 opaque colours; 1 frame; 2 layers')
