local M={}
local function valid(obj)
    local ok,value=pcall(function() return obj and obj:IsValid() end)
    return ok and value
end
local function same(a,b)
    if a==b then return true end
    local ok,value=pcall(function() return a:GetAddress()==b:GetAddress() end)
    return ok and value
end
local cyan={R=0.18,G=0.94,B=1,A=1}
local white={R=0.92,G=1,B=1,A=1}
local dark={R=0.01,G=0.045,B=0.08,A=0.82}
local WIDTH,HEIGHT,BASE=60,38,36
-- Render-only boost confirmation. Does not touch physics, cooldown or bar fill.
local PULSE_RISE,PULSE_FALL,PULSE_AMOUNT=0.10,0.46,0.28
local PULSE_TOTAL=PULSE_RISE+PULSE_FALL
local function pulseScale(age)
    if age<=PULSE_RISE then
        local t=math.max(0,math.min(1,age/PULSE_RISE))
        return 1+PULSE_AMOUNT*(1-(1-t)^3) -- inflate quickly
    end
    local t=math.max(0,math.min(1,(age-PULSE_RISE)/PULSE_FALL))
    local smooth=t*t*(3-2*t)
    return 1+PULSE_AMOUNT*(1-smooth) -- smooth return to normal
end
local heights={12,17,22,27,32}
local function slate(color) return {SpecifiedColor=color,ColorUseRule=0} end

function M.new(env)
    local self={seekAge=1,paintAge=0,failed=false,pulseAge=nil,pulseScale=1,pulseEnabled=true}
    -- Called only after boost.lua confirms the velocity write succeeded.
    -- A pulse is retained until the HUD becomes visible, even if it attaches late.
    function self:pulse()
        self.pulseAge=0
    end
    local function paintPulse(dt)
        if not self.pulseEnabled or self.pulseAge==nil then return end
        self.pulseAge=math.min(PULSE_TOTAL,self.pulseAge+math.max(0,dt))
        local scale=pulseScale(self.pulseAge)
        if math.abs(scale-self.pulseScale)>0.001 then
            -- Optional effect: never let a render-transform failure disable the HUD.
            local ok,err=pcall(function() self.root:SetRenderScale({X=scale,Y=scale}) end)
            if not ok then
                self.pulseEnabled=false
                self.pulseAge=nil
                return
            end
            self.pulseScale=scale
        end
        if self.pulseAge>=PULSE_TOTAL then self.pulseAge=nil end
    end
    local classes={}
    local function cls(name)
        if not valid(classes[name]) then classes[name]=env.static('/Script/UMG.'..name) end
        assert(valid(classes[name]),'UMG class unavailable: '..name)
        return classes[name]
    end
    local function make(name,tree)
        local obj=env.construct(cls(name),tree)
        assert(valid(obj),'Could not construct boost HUD '..name)
        return obj
    end
    function self:cleanup()
        if valid(self.root) then
            pcall(function() self.root:SetVisibility(1) end)
            local ok,err=pcall(function() self.root:RemoveFromParent() end)
            if not ok then
                return false
            end
        end
        self.root=nil;self.native=nil;self.host=nil;self.pawn=nil
        self.frames=nil;self.fills=nil;self.fillSlots=nil;self.slot=nil
        self.lastError=nil;self.lastRight=nil;self.lastBottom=nil
        self.pulseAge=nil -- cancel visual pulse if the pilot exits or HUD is removed
        self.pulseScale=1
        return true
    end
    local function screenFor(pawn)
        local pc=pawn:GetController()
        local ok,screen=pcall(function() return pc:GetHUD().HUDScreen end)
        if ok and valid(screen) and same(screen:GetOwningPlayer(),pc) then return screen end
        for _,candidate in ipairs(env.find('WBP_HUDScreen_C') or {}) do
            if valid(candidate) and same(candidate:GetOwningPlayer(),pc) and candidate:IsVisible() then return candidate end
        end
    end
    local function hostPanel(native)
        local panel=native.WidgetTree.RootWidget
        -- Follow only unambiguous single-child wrappers of the full-screen root.
        -- Never search arbitrary compact descendants for an available canvas.
        for _=1,12 do
            if not valid(panel) then break end
            if panel:IsA(cls('CanvasPanel')) then return panel,true end
            if panel:IsA(cls('Overlay')) then return panel,false end
            local ok,child=pcall(function()
                if panel:GetChildrenCount()==1 then return panel:GetChildAt(0) end
            end)
            if not ok or not valid(child) then break end
            panel=child
        end
    end
    local function add(canvas,child,x,y,w,h)
        local slot=canvas:AddChildToCanvas(child)
        assert(valid(slot) or slot~=nil,'Boost HUD canvas slot unavailable')
        slot:SetAnchors({Minimum={X=0,Y=0},Maximum={X=0,Y=0}})
        slot:SetAlignment({X=0,Y=0});slot:SetAutoSize(false)
        slot:SetPosition({X=x,Y=y});slot:SetSize({X=w,Y=h})
        return slot
    end
    local function border(canvas,tree,x,y,w,h,outlined)
        local obj=make('Border',tree)
        local slot=add(canvas,obj,x,y,w,h)
        local brush=obj.Background
        brush.DrawAs=4;brush.TintColor=slate(outlined and dark or white)
        brush.OutlineSettings.Color=slate(white);brush.OutlineSettings.Width=outlined and 1.1 or 0
        brush.OutlineSettings.RoundingType=0
        brush.OutlineSettings.CornerRadii={X=0.6,Y=0.6,Z=0.6,W=0.6}
        brush.OutlineSettings.bUseBrushTransparency=false
        obj:SetBrush(brush);obj:SetBrushColor(white)
        return obj,slot
    end
    local function create(native,pawn,host,isCanvas)
        local tree=native.WidgetTree
        local box=make('SizeBox',tree)
        self.root=box;self.native=native;self.host=host;self.pawn=pawn
        box:SetWidthOverride(WIDTH);box:SetHeightOverride(HEIGHT);box:SetVisibility(1)
        -- Scale around the center; layout size and native screen anchors stay fixed.
        self.pulseScale=1
        self.paintAge=0 -- prevent an old startup backlog from skipping a queued pulse
        local pulseOK,pulseErr=pcall(function()
            box:SetRenderTransformPivot({X=0.5,Y=0.5})
            box:SetRenderScale({X=1,Y=1})
        end)
        self.pulseEnabled=pulseOK
        if isCanvas then
            self.slot=host:AddChildToCanvas(box)
            self.slot:SetAnchors({Minimum={X=1,Y=1},Maximum={X=1,Y=1}})
            self.slot:SetAlignment({X=1,Y=1});self.slot:SetAutoSize(false)
            self.slot:SetSize({X=WIDTH,Y=HEIGHT});self.slot:SetZOrder(20)
        else
            self.slot=host:AddChildToOverlay(box)
            self.slot:SetHorizontalAlignment(3);self.slot:SetVerticalAlignment(3)
            self.slot:SetPadding({Left=0,Top=0,Right=0,Bottom=0})
        end
        self.isCanvas=isCanvas
        local canvas=make('CanvasPanel',tree)
        local content=box:AddChild(canvas)
        content:SetHorizontalAlignment(0);content:SetVerticalAlignment(0)
        self.frames={};self.fills={};self.fillSlots={}
        for i,h in ipairs(heights) do
            local x=2+(i-1)*12
            self.frames[i]=border(canvas,tree,x,BASE-h,8,h,true)
            self.fills[i],self.fillSlots[i]=border(canvas,tree,x+1,BASE-h+1,6,h-2,false)
        end
    end
    local function position(state)
        local right,bottom=state.right or 400,state.bottom or 50
        if self.lastRight==right and self.lastBottom==bottom then return end
        if self.isCanvas then self.slot:SetPosition({X=-right,Y=-bottom})
        else self.slot:SetPadding({Left=0,Top=0,Right=right,Bottom=bottom}) end
        self.lastRight=right;self.lastBottom=bottom
    end
    function self:update(pawn,state,visible,dt)
        if self.failed then self:cleanup();return end
        local ok,err=pcall(function()
            self.seekAge=self.seekAge+math.max(0,dt)
            self.paintAge=self.paintAge+math.max(0,dt)
            local screen=screenFor(pawn)
            if self.root and (not same(self.native,screen) or not valid(self.root) or not valid(self.native) or not valid(self.host)
                or not same(self.pawn,pawn) or not same(self.native:GetOwningPlayer(),pawn:GetController())) then
                if not self:cleanup() then return end
            end
            if not self.root then
                if self.seekAge<1 then return end
                self.seekAge=0
                if valid(screen) then
                    local host,isCanvas=hostPanel(screen)
                    if valid(host) then create(screen,pawn,host,isCanvas) end
                end

            end
            if not self.root then return end
            -- GetIsVisible is a deprecated viewport-membership check. Embedded
            -- vehicle HUD widgets can be visible without being viewport roots.
            if not visible then self.root:SetVisibility(1);return end
            if not self.native:IsVisible() then self.root:SetVisibility(1);return end
            if self.paintAge<0.05 then return end
            local paintDt=self.paintAge
            self.paintAge=0
            position(state)
            paintPulse(paintDt)
            self.root:SetVisibility(3) -- HitTestInvisible for the entire subtree.
            local unavailable=state.error~=nil and state.error~=false
            local progress=unavailable and 0 or math.max(0,math.min(1,state.progress))
            for i,h in ipairs(heights) do
                local fraction=math.max(0,math.min(1,progress*5-(i-1)))
                local fillHeight=(h-2)*fraction
                self.fillSlots[i]:SetSize({X=6,Y=fillHeight})
                self.fillSlots[i]:SetPosition({X=3+(i-1)*12,Y=BASE-1-fillHeight})
                self.fills[i]:SetVisibility(fraction>0 and 3 or 1)
                self.fills[i]:SetBrushColor(cyan)
                if self.lastError~=unavailable then
                    local brush=self.frames[i].Background
                    brush.OutlineSettings.Color=slate(unavailable and {R=0.4,G=0.5,B=0.55,A=0.8} or white)
                    self.frames[i]:SetBrush(brush)
                end
            end
            self.lastError=unavailable
        end)
        if not ok then
            self.failed=true;self:cleanup()
        end
    end
    return self
end
return M
