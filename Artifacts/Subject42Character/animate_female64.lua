local art='D:/fork/HelpMePlease/Artifacts/Subject42Character/'
local src=app.open(art..'Subject42_Female64_Idle.aseprite')
local s=Sprite(64,64,ColorMode.RGB)
s:setPalette(src.palettes[1])
local parts={}
for _,l in ipairs(src.layers) do
 if l.name:match('^0[1-5] ') then
  local cel=l:cel(1);local im=Image(64,64,ColorMode.RGB)
  im:drawImage(cel.image,cel.position)
  local dst=#parts==0 and s.layers[1] or s:newLayer();dst.name=l.name
  table.insert(parts,{name=l.name,image=im,layer=dst})
 end
end
assert(#parts==5)
for i=2,6 do s:newEmptyFrame() end
local left={2,-1,-3,-2,1,3}
local right={-2,1,3,2,-1,-3}
local bob={0,1,-1,0,1,-1}
local sway={-1,0,1,1,0,-1}
local function round(v) return math.floor(v+0.5) end
local function sample(im,x,y)
 if x<0 or x>63 or y<0 or y>63 then return 0 end
 return im:getPixel(x,y)
end
local function armY(y,delta)
 -- Shoulder is fixed; the forearm swings farther than the upper arm.
 if y<25 then return y end
 if y<32+delta then return round(25+(y-25)*7/(7+delta)) end
 return y-delta
end
for f=1,6 do
 s.frames[f].duration=0.1
 for _,p in ipairs(parts) do
  local im=Image(64,64,ColorMode.RGB)
  for y=0,63 do for x=0,63 do
   local sx,sy=x,y-bob[f]
   if p.name:match('^01') then
    local isLeft=x<33
    local step=isLeft and left[f] or right[f]
    local passing=(f==2 or f==5)
    local shift=passing and (isLeft and 1 or -1) or 0
    -- Hips stay under skirt. Knee flexion shortens the trailing leg;
    -- the boot itself stays rigid, translated along the ground plane.
    local bootY=50+step
    if y<bootY then
     sy=round(40+(y-40)*10/(10+step))
     sx=x-round(shift*math.max(0,math.min(1,(y-40)/10)))
    else sy=y-step; sx=x-shift end
   elseif p.name:match('^02') then
    local delta=x<25 and round(-left[f]*0.65) or (x>=40 and round(-right[f]*0.65) or 0)
    if delta~=0 then sy=armY(y-bob[f],delta) end
   elseif p.name:match('^03') then
    sx=x-round(sway[f]*math.max(0,math.min(1,(sy-34)/9)))
   elseif p.name:match('^05') and x>=40 then
    sy=armY(y-bob[f],round(-right[f]*0.65))
   end
   im:drawPixel(x,y,sample(p.image,sx,sy))
  end end
  s:newCel(p.layer,f,im,Point(0,0))
 end
end
local tag=s:newTag(1,6);tag.name='Walk_Down'
s:saveAs(art..'Subject42_Female64_Walk.aseprite')
local check=app.open(art..'Subject42_Female64_Walk.aseprite')
assert(check.width==64 and check.height==64 and #check.frames==6 and #check.layers==5)
local signatures={}
local report=io.open(art..'Female64_Walk_validation.txt','w')
for f=1,6 do
 local im=Image(64,64,ColorMode.RGB);im:drawSprite(check,f)
 local colors={};local n,count=0,0;local signature={}
 for pixel in im:pixels() do
  local v=pixel(); local a=app.pixelColor.rgbaA(v);assert(a==0 or a==255)
  if a==255 then
   count=count+1
   assert(pixel.x>0 and pixel.x<63 and pixel.y>0 and pixel.y<63,'Clipped edge')
   if not colors[v] then colors[v]=true;n=n+1 end
  end
  signature[#signature+1]=tostring(v)
 end
 assert(n==10 and count>500)
 local key=table.concat(signature,',');assert(not signatures[key],'Duplicate frame');signatures[key]=true
 report:write('Frame '..f..': 64x64, 100 ms, '..n..' colors, '..count..' opaque pixels, binary alpha, no edge clipping, unique pose\n')
end
report:write('6 frames, 5 editable layers, Walk_Down loop = 600 ms. Original idle unchanged.\n');report:close()
local board=Sprite(384,64,ColorMode.RGB)
local row=Image(384,64,ColorMode.RGB)
for f=1,6 do local im=Image(64,64,ColorMode.RGB);im:drawSprite(check,f);row:drawImage(im,Point((f-1)*64,0)) end
board:newCel(board.layers[1],1,row,Point(0,0));board:resize(1152,192);board:saveCopyAs(art..'Female64_Walk_Frames.png')
check:resize(384,384);check:saveCopyAs(art..'Female64_Walk_Preview.gif')
