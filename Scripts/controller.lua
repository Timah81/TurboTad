local M={}
local function valid(obj)
    local ok,result=pcall(function() return obj and obj:IsValid() end)
    return ok and result
end
local function same(a,b)
    if a==b then return true end
    local ok,result=pcall(function() return a:GetAddress()==b:GetAddress() end)
    return ok and result
end

function M.new(core,config)
    local self={target=nil,ownerPC=nil,changes={},parked={},injecting=false,restoreErrors={}}

    function self:owns(pawn)
        return self.target~=nil and same(self.target,pawn)
    end

    local function restore(changes)
        local pending,errors={},{}
        for i=#changes,1,-1 do
            local change=changes[i]
            if valid(change.obj) then
                local ok,err=pcall(function() change.obj[change.name]=change.original end)
                if not ok then
                    pending[#pending+1]=change;errors[#errors+1]=change.name..': '..tostring(err)
                end
            end
        end
        return pending,errors
    end

    function self:attached(pawn)
        return valid(pawn) and valid(pawn.AttachedToOwner)
    end

    function self:release(keepOrientation)
        local previous=self.target
        self.target=nil;self.ownerPC=nil;self.injecting=false
        if keepOrientation and valid(previous) and not self:attached(previous) then
            local retained,other={},{}
            for _,change in ipairs(self.changes) do
                if same(change.obj,previous) and (change.name=='RollCorrectionRate' or change.name=='PitchCorrectionRate') then
                    retained[#retained+1]=change
                else other[#other+1]=change end
            end
            if #retained>0 then self.parked[#self.parked+1]={pawn=previous,changes=retained} end
            self.changes=other
        end
        self.changes,self.restoreErrors=restore(self.changes)
        return #self.restoreErrors==0
    end

    function self:maintainParked(pawn)
        for i=#self.parked,1,-1 do
            local record=self.parked[i]
            local p=record.pawn
            if not valid(p) then table.remove(self.parked,i)
            elseif pawn==nil or same(p,pawn) then
                if record.restoring or self:attached(p) or (p:IsPiloted() and not p:IsLocallyControlled()) then
                    record.restoring=true
                    record.changes=restore(record.changes)
                    if #record.changes==0 then table.remove(self.parked,i) end
                elseif not p.bIsAttaching then
                    -- No transforms, velocities, camera fields or movement commands while parked.
                    p.RollCorrectionRate=0;p.PitchCorrectionRate=0
                end
            end
        end
    end

    function self:reset()
        self:release()
        for _,record in ipairs(self.parked) do record.restoring=true end
        self:maintainParked()
        return #self.changes==0 and #self.parked==0
    end

    local function save(obj,name,newValue)
        local old=obj[name]
        if old==nil then error('Required field unavailable: '..name) end
        self.changes[#self.changes+1]={obj=obj,name=name,original=old}
        obj[name]=newValue
    end

    function self:possessed(pawn)
        if not valid(pawn) or not pawn:IsLocallyControlled() or not pawn:IsPiloted() then return false end
        local pc=pawn:GetController()
        return valid(pc) and same(pc:K2_GetPawn(),pawn)
    end

    function self:eligible(pawn)
        if not self:possessed(pawn) or not pawn:GetIsPowered() or pawn:IsBroken() then return false end
        return not self:attached(pawn) and not pawn.bIsAttaching
    end

    function self:inputAllowed(pawn)
        local pc=pawn:GetController()
        -- Possession can change after eligible() checked the controller.
        -- Treat a missing/expired controller as blocked input for this frame.
        return valid(pc) and not pc.bShowMouseCursor
            and not pc:IsMoveInputIgnored() and not pc:IsLookInputIgnored()
    end

    function self:step(pawn,axes,settings)
        local ok,result=pcall(function()
            if not axes or not self:eligible(pawn) then
                self:release(self.target and not self:possessed(self.target));return false
            end
            local pc=pawn:GetController()
            if not self:owns(pawn) then
                self:release(self.target and not self:possessed(self.target))
                if #self.changes>0 then error('Previous vehicle settings have not restored; restart the game') end
                self.target=pawn
                self.ownerPC=pc
                local adopted=false
                for i=#self.parked,1,-1 do
                    local record=self.parked[i]
                    if same(record.pawn,pawn) then
                        if record.restoring then error('Parked settings have not restored; restart the game') end
                        for _,change in ipairs(record.changes) do self.changes[#self.changes+1]=change end
                        table.remove(self.parked,i);adopted=true;break
                    end
                end
                if not adopted then
                    save(pawn,'RollCorrectionRate',0)
                    save(pawn,'PitchCorrectionRate',0)
                end
                save(pawn,'DefaultBankingModifier',0)
                local camera=pawn.FirstPersonCamera
                if not valid(camera) then error('Tadpole camera unavailable') end
                save(camera,'bUsePawnControlRotation',false)
            end
            -- Chassis changes may reapply these rates; keep the active instance unlocked.
            pawn.RollCorrectionRate=0
            pawn.PitchCorrectionRate=0
            pawn.DefaultBankingModifier=0
            if not self:inputAllowed(pawn) then axes={0,0,0,0,0,0} end
            settings=settings or {}
            local speedScale=settings.TopSpeedScale or 1
            if type(speedScale)~='number' or speedScale~=speedScale or speedScale<0.25 or speedScale>4 then
                error('Invalid top speed multiplier')
            end
            local cm=pawn.CharacterMovement
            local speedChange=nil
            for _,change in ipairs(self.changes) do
                if change.name=='MaxSwimSpeed' and same(change.obj,cm) then speedChange=change;break end
            end
            if speedChange then
                cm.MaxSwimSpeed=speedChange.original*speedScale
            elseif speedScale~=1 then
                if not valid(cm) then error('Tadpole movement component unavailable') end
                local original=cm.MaxSwimSpeed
                if type(original)~='number' or original~=original or original<=0 or original==math.huge then
                    error('Native swimming speed unavailable')
                end
                save(cm,'MaxSwimSpeed',original*speedScale)
            end
            local world=core.world_thrust(axes,pawn:GetActorForwardVector(),
                pawn:GetActorRightVector(),pawn:GetActorUpVector(),settings.ForwardScale,settings.StrafeScale,settings.VerticalScale)
            pawn:ConsumeMovementInputVector()
            pawn:AddMovementInput(world,1,false)
            self.injecting=true
            local s=config.RotationInputScale or 1
            pawn:AddInputRotator({Pitch=axes[4]*(settings.PitchScale or s),
                Yaw=axes[5]*(settings.YawScale or s),Roll=axes[6]*(settings.RollScale or s)})
            self.injecting=false
            return true
        end)
        if not ok then self:release();error(result) end
        return result
    end
    return self
end
return M
