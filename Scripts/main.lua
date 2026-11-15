-- Personal audio test build. T6S2 publishes a successful boost token to the companion.
print('[TurboTad] 0.3.15A BOOST SOUND TRIGGER loaded; status protocol T6S2 active.')
local source=debug.getinfo(1,'S').source:gsub('^@','')
local scripts=assert(source:match('^(.*[/\\])'),'Cannot locate mod script directory')
local core=dofile(scripts..'core.lua')
local config=dofile(scripts..'config.lua')
local controller=dofile(scripts..'controller.lua').new(core,config)
local velocity=dofile(scripts..'velocity.lua').new(scripts,config)
local topSpeedScale=1
local boost=dofile(scripts..'boost.lua').new(function(pawn,axes,settings)
    return velocity:trigger(pawn,topSpeedScale,axes,settings)
end)
local inputPath=scripts..'../input.txt'
local hooked={}
local monitor=core.new_monitor()
local nativeHook=false
local failed=false
local wasActive=false
local statusAge=0
local hud=dofile(scripts..'hud.lua').new({find=FindAllOf,static=StaticFindObject,
    construct=StaticConstructObject,text=FText})
local timePawn,gameplayStatics=nil,nil
local function valid(obj)
    local ok,v=pcall(function() return obj and obj:IsValid() end)
    return ok and v
end
local function cooldownNow(pawn)
    if valid(pawn) then timePawn=pawn end
    if not valid(gameplayStatics) then gameplayStatics=StaticFindObject('/Script/Engine.Default__GameplayStatics') end
    local ok,real=pcall(function() return gameplayStatics:GetRealTimeSeconds(timePawn) end)
    local worldOK,world=pcall(function() return timePawn:GetWorld():GetAddress() end)
    return boost:time(ok and real or nil,os.time(),worldOK and world or nil)
end
local function writeBoostStatus(active)
    pcall(function()
        local now=os.time()
        local remaining=boost:remaining(cooldownNow())
        local code=failed and 4 or (boost.error and 2 or (not active and 3 or (remaining>0 and 1 or 0)))
        local f=io.open(scripts..'../boost-status.txt','w')
        if f then f:write('T6S2 '..now..' '..remaining..' '..code..' '..(boost.lastRequested or 0)..'\n');f:close() end
    end)
end
local function unwrap(v)
    if type(v)=='number' then return v end
    return v:get()
end
local function readFrame()
    local f=io.open(inputPath,'r')
    if not f then return nil end
    local text=f:read(513)
    f:close()
    return core.parse(text,os.time())
end
local function onTick(context,delta)
    if failed then return end
    local ok,err=pcall(function()
        local pawn=context:get()
        controller:maintainParked(pawn)
        -- Do not release another local pawn's control when a remote Tadpole ticks.
        if not pawn:IsLocallyControlled() then
            if controller:owns(pawn) then controller:release(not pawn:IsPiloted()) end
            if hud.pawn and not controller:possessed(hud.pawn) then hud:cleanup() end
            return
        end
        local dt=tonumber(unwrap(delta)) or 0
        if dt<=0 then return end
        local axes=core.update(monitor,readFrame(),dt,config.InputTimeoutSeconds)
        local frame=monitor.frame
        local settings=frame and frame.settings or nil
        if frame and not frame.active then controller:reset() end
        local active=controller:step(pawn,axes,settings)
        local acceptingInput=active and not frame.suspended and controller:inputAllowed(pawn)
        local now=cooldownNow(pawn)
        if acceptingInput then
            topSpeedScale=settings.TopSpeedScale or 1
            local token=frame and frame.boost or 0
            if not wasActive then boost:sync(frame.boost) end
            if boost:request(pawn,frame.boost,settings.BoostCooldownSeconds,now,axes,settings) then
                -- The companion hears a confirmed burst, not a mere button press.
                writeBoostStatus(true)
                hud:pulse() -- visual confirmation only, after the velocity burst succeeds
            end
        elseif frame then boost:sync(frame.boost) end
        if active and config.EnableBoostHud then
            hud:update(pawn,{progress=boost:progress(now),remaining=math.max(0,boost.nextAt-now),error=boost.error,right=settings.HudRight,bottom=settings.HudBottom},acceptingInput,dt)
        else hud:cleanup() end
        statusAge=statusAge+dt
        if statusAge>=0.25 or active~=wasActive then writeBoostStatus(acceptingInput);statusAge=0 end
        if active~=wasActive then
            wasActive=active
        end
    end)
    if not ok then
        controller:reset();failed=true
        hud:cleanup()
        writeBoostStatus(false)
    end
end
-- Suppress the stock look/banking input only while this mod owns the local vehicle.
local ok,err=pcall(function()
    RegisterHook('/Script/Subnautica2.SN2Tadpole:AddInputRotator',function(context,rotator)
        local pawn=context:get()
        if controller:owns(pawn) and not controller:eligible(pawn) then controller:release(not controller:possessed(pawn)) end
        if not controller.injecting and controller:owns(pawn) then
            local r=rotator:get()
            r.Pitch=0;r.Yaw=0;r.Roll=0
        end
    end)
    -- A chassis banking modifier is read by the stock look ability.
    RegisterHook('/Script/Subnautica2.SN2Tadpole:GetBankingModifier',function(context)
        local pawn=context:get()
        if controller:owns(pawn) and not controller:eligible(pawn) then controller:release(not controller:possessed(pawn)) end
        if controller:owns(pawn) then return 0 end
    end)
    nativeHook=true
    RegisterHook('/Script/Subnautica2.SN2PlayerController:OnPossessedPawnChangedFunction',function(context)
        if controller.target and not controller:possessed(controller.target) then
            if controller.target and not controller:possessed(controller.target) then controller:release(true) end
            monitor=core.new_monitor();wasActive=false
            hud:cleanup()
            writeBoostStatus(false)
        end
    end)
end)
if not ok then
    failed=true;do end
    writeBoostStatus(false)
end

if nativeHook then
    LoopAsync(1000,function()
        if failed then
            ExecuteInGameThread(function()
                if #controller.changes>0 or #controller.parked>0 then controller:reset() end
                hud:cleanup()
                writeBoostStatus(false)
            end)
            return false
        end
        ExecuteInGameThread(function()
            local success,problem=pcall(function()
                controller:maintainParked()
                if hud.pawn and not controller:possessed(hud.pawn) then hud:cleanup() end
                local frame=readFrame()
                if frame and not frame.active then controller:reset();hud:cleanup() end
                if controller.target then
                    local target=controller.target
                    if not controller:eligible(target) or (frame and not frame.active) then
                        controller:release(not controller:possessed(target))
                        hud:cleanup()
                        boost:sync(frame and frame.boost or 0)
                        wasActive=false
                        writeBoostStatus(false)
                    end
                end
                -- DIAGNOSTIC: Only scan the global UObject array until the Tadpole
                -- class hooks have been registered. A fresh launch restores discovery.
                if not next(hooked) then
                    local pawns=FindAllOf('BP_Tadpole_C') or {}
                    for _,pawn in ipairs(pawns) do
                        if pawn:IsValid() then
                            local cls=pawn:GetClass()
                            local classPath=cls:GetFullName():gsub('^%S+%s+','')
                            if not hooked[classPath] then
                                RegisterHook(classPath..':ReceiveTick',onTick)
                                RegisterHook(classPath..':OnPilotExited_BP',function(context)
                                    if controller:owns(context:get()) then
                                        if controller:owns(context:get()) then controller:release(true) end
                                        monitor=core.new_monitor();wasActive=false
                                        hud:cleanup()
                                        writeBoostStatus(false)
                                    end
                                end)
                                hooked[classPath]=true
                            end
                        end
                    end
                end
            end)
            if not success then
                controller:reset();failed=true
                hud:cleanup()
                writeBoostStatus(false)
            end
        end)
        return false
    end)
end
