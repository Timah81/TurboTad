local baselineDir, runtimeDir = assert(arg[1]), assert(arg[2])
local baseline = dofile(baselineDir .. '/controller.lua')
local core = dofile(runtimeDir .. '/core.lua')
local controller = dofile(runtimeDir .. '/controller.lua')
local config = dofile(runtimeDir .. '/config.lua')
local keyboard = dofile(runtimeDir .. '/keyboard_input.lua')

local checks = 0
local function check(value, description)
    assert(value, description)
    checks = checks + 1
end
local function near(a, b) return math.abs(a-b) < 1e-12 end
local function makeVehicle()
    local function valid() return true end
    local pc = {IsValid=valid, bShowMouseCursor=false,
        IsLookInputIgnored=function() return false end,
        IsMoveInputIgnored=function() return false end}
    local pawn = {
        IsValid=valid, IsLocallyControlled=valid, IsPiloted=valid, GetIsPowered=valid,
        IsBroken=function() return false end, bIsAttaching=false,
        GetController=function() return pc end,
        RollCorrectionRate=1, PitchCorrectionRate=1, DefaultBankingModifier=1,
        FirstPersonCamera={IsValid=valid, bUsePawnControlRotation=true},
        CharacterMovement={IsValid=valid, MaxSwimAcceleration=1200, MaxSwimSpeed=1000},
        GetActorForwardVector=function() return {X=1,Y=0,Z=0} end,
        GetActorRightVector=function() return {X=0,Y=1,Z=0} end,
        GetActorUpVector=function() return {X=0,Y=0,Z=1} end,
        ConsumeMovementInputVector=function() end,
        AddMovementInput=function(self,v) self.translation=v end,
        AddInputRotator=function(self,v) self.rotation=v end
    }
    pc.K2_GetPawn=function() return pawn end
    return pawn,pc
end

local old, new = baseline.new(core,config), controller.new(core,config)
local oldPawn = makeVehicle()
local pawn,pc = makeVehicle()
local base = {PitchScale=7, YawScale=4, RollScale=2.71, TopSpeedScale=2, ForwardScale=3}
local axes = {0.2,-0.3,0.4,0.25,-0.5,0.75}
local rotation = {'Pitch','Yaw','Roll'}
for _, value in ipairs({-1,-0.75,-0.01,0,0.01,0.75,1}) do
    local input={axes[1],axes[2],axes[3],value,-value,value}
    old:step(oldPawn,input,base)
    new:step(pawn,input,base)
    for _, axis in ipairs(rotation) do
        check(pawn.rotation[axis] == oldPawn.rotation[axis], 'exact legacy angular trace')
    end
    for i, axis in ipairs(rotation) do base[axis..'Gain'] = 1 end
    new:step(pawn,input,base)
    for _, axis in ipairs(rotation) do
        check(pawn.rotation[axis] == oldPawn.rotation[axis], 'exact explicit neutral angular trace')
    end
end

old:step(oldPawn,axes,base)
for i, axis in ipairs(rotation) do
    local gainKey=axis..'Gain'
    for _, gain in ipairs({0.1,3,10}) do
        base[gainKey] = gain
        new:step(pawn,axes,base)
        for _, other in ipairs(rotation) do
            if other==axis then
                local magnitude=math.abs(axes[i+3])
                check(near(pawn.rotation[other], oldPawn.rotation[other] *
                    gain/(1+(gain-1)*magnitude)), 'independent partial-stick response')
            else
                check(pawn.rotation[other]==oldPawn.rotation[other], 'other angular axes unchanged')
            end
        end
        check(pawn.translation.X==oldPawn.translation.X and pawn.translation.Y==oldPawn.translation.Y
            and pawn.translation.Z==oldPawn.translation.Z, 'translation unchanged')
        check(pawn.CharacterMovement.MaxSwimAcceleration==oldPawn.CharacterMovement.MaxSwimAcceleration
            and pawn.CharacterMovement.MaxSwimSpeed==oldPawn.CharacterMovement.MaxSwimSpeed, 'speed and acceleration unchanged')
    end
    base[gainKey]=1
end
base.PitchGain=0.1;base.YawGain=10;base.RollGain=3
for _, value in ipairs({-1,0,1}) do
    local endpoints={0,0,0,value,-value,value}
    old:step(oldPawn,endpoints,base)
    new:step(pawn,endpoints,base)
    for _, axis in ipairs(rotation) do
        check(pawn.rotation[axis]==oldPawn.rotation[axis], 'full deflection, digital and zero unchanged')
    end
end
pc.bShowMouseCursor=true
new:step(pawn,axes,base)
check(pawn.rotation.Pitch==0 and pawn.rotation.Yaw==0 and pawn.rotation.Roll==0, 'blocked input stays neutral')
pc.bShowMouseCursor=false
check(not new:step(pawn,nil,base) and pawn.CharacterMovement.MaxSwimSpeed==1000, 'stale input restores settings')

local stamp=1700000000
local function frame(version,seq,mode,extra)
    return core.parse(string.format('%s %d %d %d 0 0 0 0.25 -0.5 0.75 7 4 2.71 0 10 1 1 1 400 50 2%s',
        version,seq,stamp,mode,extra or ''),stamp,keyboard.validName)
end
local noKeys=' - - - - - - - - - - - - LeftShift'
local modern=assert(frame('T6D9',2,1,' 0.1 10 3'))
check(modern.keyboard==nil and modern.settings.PitchGain==0.1 and modern.settings.YawGain==10
    and modern.settings.RollGain==3, 'analog T6D9 frame')
local mixed=assert(frame('T6D9',3,1,noKeys..' 1 2 3'))
check(mixed.keyboard[13]=='LeftShift' and mixed.settings.YawGain==2, 'mixed T6D9 frame')
local suspended=assert(frame('T6D9',4,2,noKeys..' 1 2 3'))
check(suspended.suspended and suspended.axes[4]==0 and suspended.boost==0, 'suspended mixed frame')
check(assert(frame('T6D8',1,1,noKeys)).settings.PitchGain==nil, 'legacy keyboard stays neutral')
check(assert(frame('T6D7',1,1)).settings.PitchGain==nil, 'legacy analog stays neutral')
for _, malformed in ipairs({' 0 1 1',' 1 10.01 1',' 1 1 nan',' 1 1',' 1 1 1 1',noKeys..' 1 1'}) do
    check(not frame('T6D9',2,1,malformed), 'reject malformed gain frame')
end

local monitor=core.new_monitor()
check(not core.update(monitor,frame('T6D7',1,1),0.01,0.25), 'startup needs advancing frames')
local accepted=core.update(monitor,modern,0.01,0.25)
new:step(pawn,accepted,monitor.frame.settings)
local previous=pawn.rotation.Pitch
accepted=core.update(monitor,frame('T6D9',3,1,' 10 10 3'),0.01,0.25)
new:step(pawn,accepted,monitor.frame.settings)
check(pawn.rotation.Pitch>previous, 'live gain affects next accepted frame')
accepted=core.update(monitor,frame('T6D7',4,1),0.01,0.25)
new:step(pawn,accepted,monitor.frame.settings)
check(pawn.rotation.Pitch==0.25*7, 'live return to 100% restores exact linear response')
check(not core.update(monitor,nil,0.26,0.25), 'gain frames still time out')
print('Rotational response passed: '..checks..' assertions (actual baseline and new controller, mocked vehicle).')
