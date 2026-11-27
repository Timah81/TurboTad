local M = {}
local function finite(n)
    return type(n) == 'number' and n == n and n ~= math.huge and n ~= -math.huge
end

function M.parse(line, now, validKeyName)
    if type(line) ~= 'string' or #line > 512 then return nil end
    local words = {}
    for word in line:gmatch('%S+') do words[#words+1] = word end
    local gains=words[1]=='T6D9'
    local keyboard=words[1]=='T6D8' or (gains and #words==37)
    local speed=words[1]=='T6D7' or keyboard or gains
    local screen=words[1]=='T6D6' or speed
    local translation=words[1]=='T6D5' or screen
    local throttle=words[1]=='T6D4' or translation
    local modern=words[1]=='T6D3' or throttle
    local extended=words[1]=='T6D2' or modern
    local expected=keyboard and 34 or (speed and 21 or (screen and 20 or (translation and 18 or (throttle and 16 or 15))))
    if gains then expected=expected+3 end
    if not ((words[1]=='T6D1' and #words==10) or (extended and #words==expected)) then return nil end
    local seq, stamp, active = tonumber(words[2]), tonumber(words[3]), tonumber(words[4])
    if not finite(seq) or seq < 0 or seq % 1 ~= 0 then return nil end
    if not finite(stamp) or stamp < 0 or stamp % 1 ~= 0 then return nil end
    if math.abs(now-stamp) > 2 or (active ~= 0 and active ~= 1 and not (modern and active==2)) then return nil end
    local axes = {}
    for i=1,6 do
        local v = tonumber(words[i+4])
        if not finite(v) or v < -1 or v > 1 then return nil end
        axes[i] = v
    end
    local settings={PitchScale=1,YawScale=1,RollScale=1,ForwardScale=1,StrafeScale=1,VerticalScale=1,BoostCooldownSeconds=10,HudRight=400,HudBottom=50}
    local boost=0
    if extended then
        local pitch,yaw,roll=tonumber(words[11]),tonumber(words[12]),tonumber(words[13])
        boost=tonumber(words[14])
        local cooldown=tonumber(words[15])
        for _,v in ipairs({pitch or false,yaw or false,roll or false}) do
            if not finite(v) or v<0.1 or v>(modern and 10 or 4) then return nil end
        end
        if not finite(boost) or boost<0 or boost%1~=0 then return nil end
        if not finite(cooldown) or cooldown<2 or cooldown>60 then return nil end
        local forward,strafe,vertical=1,1,1
        if throttle then forward=tonumber(words[16]) end
        if translation then strafe=tonumber(words[17]);vertical=tonumber(words[18]) end
        for _,v in ipairs({forward or false,strafe or false,vertical or false}) do
            if not finite(v) or v<0.1 or v>10 then return nil end
        end
        settings={PitchScale=pitch,YawScale=yaw,RollScale=roll,ForwardScale=forward,StrafeScale=strafe,VerticalScale=vertical,BoostCooldownSeconds=cooldown,HudRight=400,HudBottom=50}
    end
    if screen then
        local right,bottom=tonumber(words[19]),tonumber(words[20])
        if not finite(right) or right<0 or right>2000 or not finite(bottom) or bottom<0 or bottom>2000 then return nil end
        settings.HudRight=right;settings.HudBottom=bottom
    end
    settings.TopSpeedScale=1
    if speed then
        local scale=tonumber(words[21])
        if not finite(scale) or scale<0.25 or scale>4 then return nil end
        settings.TopSpeedScale=scale
    end
    local keys=nil
    if keyboard then
        if type(validKeyName)~='function' then return nil end
        keys={}
        local used={}
        for i=1,13 do
            local key=words[21+i]
            if not validKeyName(key) or (key~='-' and used[key]) then return nil end
            keys[i]=key
            if key~='-' then used[key]=true end
        end
        -- A mixed pair has exactly one non-keyboard direction. A fully
        -- keyboard pair must have zero companion contribution.
        for i=1,6 do
            local neg,pos=keys[2*i-1]~='-',keys[2*i]~='-'
            if (neg and pos and axes[i]~=0) or (neg and axes[i]<0)
                or (pos and axes[i]>0) then return nil end
            if (neg or pos) and axes[i]%1~=0 then return nil end
        end
        if keys[13]~='-' and boost~=0 then return nil end
    end
    if gains then
        local pitch,yaw,roll=tonumber(words[#words-2]),tonumber(words[#words-1]),tonumber(words[#words])
        for _,value in ipairs({pitch or false,yaw or false,roll or false}) do
            if not finite(value) or value<0.1 or value>10 then return nil end
        end
        settings.PitchGain=pitch;settings.YawGain=yaw;settings.RollGain=roll
    end
    local suspended=modern and active==2
    if suspended then axes={0,0,0,0,0,0};boost=0 end
    return {seq=seq, stamp=stamp, active=active~=0, suspended=suspended, axes=axes,settings=settings,boost=boost,keyboard=keys}
end

function M.new_monitor()
    return {age=math.huge, seq=nil, stamp=nil, frame=nil, ready=false}
end

function M.update(monitor, frame, dt, timeout)
    monitor.age = monitor.age + math.max(0, dt)
    if frame then
        if frame.seq ~= monitor.seq or frame.stamp ~= monitor.stamp then
            monitor.ready = monitor.age <= timeout and monitor.frame~=nil
                and monitor.frame.active and frame.active
            monitor.seq, monitor.stamp = frame.seq, frame.stamp
            monitor.age, monitor.frame = 0, frame
        elseif not frame.active then
            monitor.frame = frame
        end
    end
    if monitor.age > timeout then monitor.ready=false end
    if not monitor.ready or not monitor.frame or not monitor.frame.active then return nil end
    return monitor.frame.axes
end

function M.world_thrust(axes, forward, right, up, forwardScale, strafeScale, verticalScale)
    local longitudinal=axes[1]*(forwardScale or 1)
    local lateral=axes[2]*(strafeScale or 1)
    local vertical=axes[3]*(verticalScale or 1)
    return {
        X=forward.X*longitudinal+right.X*lateral+up.X*vertical,
        Y=forward.Y*longitudinal+right.Y*lateral+up.Y*vertical,
        Z=forward.Z*longitudinal+right.Z*lateral+up.Z*vertical,
    }
end

return M
