-- TurboTad self-contained velocity boost, aimed along commanded local translation.
-- No Gateway Spire objects, class loads or movement-mode changes.
local M={}
local function finite(x)
    return type(x)=='number' and x==x and x<math.huge and x>-math.huge
end
local function vec(v)
    if not v then return nil end
    local x,y,z=tonumber(v.X),tonumber(v.Y),tonumber(v.Z)
    if not finite(x) or not finite(y) or not finite(z) then return nil end
    return {X=x,Y=y,Z=z}
end
local function magnitude(v)
    return math.sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z)
end
local function dot(a,b)
    return a.X*b.X+a.Y*b.Y+a.Z*b.Z
end
local function unit(v)
    local length=magnitude(v)
    if not finite(length) or length<1e-6 then return nil end
    return {X=v.X/length,Y=v.Y/length,Z=v.Z/length}
end
local function commanded_direction(pawn,axes,settings)
    local forward=vec(pawn:GetActorForwardVector())
    if not forward then return nil,'Could not read Tadpole forward vector' end
    if type(axes)~='table' then return nil,'no translation command' end
    settings=settings or {}
    local forwardScale=settings.ForwardScale or 1
    local strafeScale=settings.StrafeScale or 1
    local verticalScale=settings.VerticalScale or 1
    if not finite(forwardScale) or not finite(strafeScale) or not finite(verticalScale)
        or forwardScale<=0 or strafeScale<=0 or verticalScale<=0 then
        return nil,'Invalid translation scale'
    end
    local f=unit(forward)
    local rightVector=vec(pawn:GetActorRightVector())
    local upVector=vec(pawn:GetActorUpVector())
    local right=rightVector and unit(rightVector)
    local up=upVector and unit(upVector)
    if not f or not right or not up then return nil,'Could not read Tadpole local axes' end
    local x,y,z=tonumber(axes[1]),tonumber(axes[2]),tonumber(axes[3])
    if not finite(x) or not finite(y) or not finite(z) then return nil,'Invalid translation command' end
    local direction={
        X=f.X*x*forwardScale+right.X*y*strafeScale+up.X*z*verticalScale,
        Y=f.Y*x*forwardScale+right.Y*y*strafeScale+up.Y*z*verticalScale,
        Z=f.Z*x*forwardScale+right.Z*y*strafeScale+up.Z*z*verticalScale,
    }
    local normalized=unit(direction)
    if not normalized then return nil,'zero translation command' end
    return normalized
end
local function valid(o)
    local ok,result=pcall(function() return o and o:IsValid() end)
    return ok and result and true or false
end
function M.new(scripts,config)
    local self={targetSpeed=config.VelocityBoostCmPerSecond or 4200}
    function self:trigger(pawn,speedScale,axes,settings)
        speedScale=speedScale or 1
        if not finite(speedScale) or speedScale<0.25 or speedScale>4 then return false,'Invalid top speed multiplier' end
        local targetSpeed=self.targetSpeed*speedScale
        if not pawn:HasAuthority() then return false,'Velocity boost requires solo play or host authority' end
        local cm=pawn.CharacterMovement
        if not valid(cm) then return false,'Tadpole movement component unavailable' end
        local mode=cm.MovementMode
        if type(mode)=='number' and mode~=4 then
            return false,'Tadpole not in swimming mode (mode '..tostring(mode)..')'
        end
        local before=vec(cm.Velocity)
        if not before then return false,'Could not read movement velocity' end
        local direction,reason=commanded_direction(pawn,axes,settings)
        if not direction then
            -- Consume the normal cooldown and keep successful boost signaling,
            -- but never change velocity if translation input is neutral.
            if reason=='zero translation command' or reason=='no translation command' then
                return true
            end
            return false,reason or 'Tadpole direction invalid'
        end
        local projectedSpeed=dot(before,direction)
        local increment=math.max(0,targetSpeed-projectedSpeed)
        local target={X=before.X+direction.X*increment,
                      Y=before.Y+direction.Y*increment,
                      Z=before.Z+direction.Z*increment}
        if not vec(target) or magnitude(target)>20000 then
            return false,'Computed velocity boost outside safety bounds'
        end
        -- UE4SS exposes FVector struct members as writable reflected properties.
        -- Avoid LaunchCharacter: it forces movement mode Falling on the next tick.
        local ok,err=pcall(function()
            local velocity=cm.Velocity
            velocity.X=target.X;velocity.Y=target.Y;velocity.Z=target.Z
        end)
        if not ok then
            pcall(function() local v=cm.Velocity;v.X=before.X;v.Y=before.Y;v.Z=before.Z end)
            return false,'Direct velocity write failed: '..tostring(err)
        end
        local after=vec(cm.Velocity)
        if not after or math.abs(after.X-target.X)>2 or math.abs(after.Y-target.Y)>2 or math.abs(after.Z-target.Z)>2 then
            pcall(function() local v=cm.Velocity;v.X=before.X;v.Y=before.Y;v.Z=before.Z end)
            return false,'Movement component did not retain velocity write'
        end
        return true
    end
    return self
end
return M
