-- TurboTad axis mapping details probe, v2; standalone READ ONLY.
-- Does not mutate assets, input mappings, player input, controller or TurboTad.
local path = debug.getinfo(1, 'S').source:gsub('^@', '')
local dir = path:match('^(.*[/\\])') or ''
local log = dir .. '..\\TurboTadAxisDetailsProbe.log'
local ticks, finished, warned = 0, false, false

local function emit(msg)
    local s = '[TT-AxisDetails] ' .. tostring(msg)
    print(s .. '\n')
    pcall(function()
        local f = io.open(log, 'a')
        if f then f:write(os.date('!%Y-%m-%dT%H:%M:%SZ '), s, '\n'); f:close() end
    end)
end
local function valid(o)
    local ok, v = pcall(function() return o and o:IsValid() end)
    return ok and v
end
local function full(o)
    if not valid(o) then return '<unavailable>' end
    local ok, s = pcall(function() return o:GetFullName() end)
    return ok and tostring(s) or '<name unreadable>'
end
local function attr(o, k)
    local ok, value = pcall(function() return o[k] end)
    if not ok or value == nil then return '<unavailable>' end
    local ok2, result = pcall(function()
        if type(value) == 'userdata' and value.ToString then return value:ToString() end
        return tostring(value)
    end)
    return ok2 and result or '<unprintable>'
end
local function arrayCount(o)
    if o == nil then return 0 end
    local ok, n = pcall(function() return #o end)
    if ok and type(n) == 'number' then return n end
    ok, n = pcall(function() return o:GetArrayNum() end)
    return ok and tonumber(n) or 0
end
local function arrayProp(o, key)
    local ok, a = pcall(function() return o[key] end)
    return ok and a or nil
end
local function mods(owner, key, prefix)
    local array = arrayProp(owner, key)
    if not array then emit(prefix .. '.' .. key .. ' inaccessible'); return end
    local count = arrayCount(array)
    emit(prefix .. '.' .. key .. ' count=' .. tostring(count))
    for j = 1, math.min(count, 12) do
        local ok, item = pcall(function() return array[j] end)
        if not ok then emit(prefix .. '.' .. key .. '[' .. j .. '] unavailable')
        else
            emit(prefix .. '.' .. key .. '[' .. j .. ']=' .. full(item) ..
                ' Order=' .. attr(item,'Order') ..
                ' bX=' .. attr(item,'bX') .. ' bY=' .. attr(item,'bY') ..
                ' bZ=' .. attr(item,'bZ') ..
                ' Scalar=' .. attr(item,'Scalar') ..
                ' LowerThreshold=' .. attr(item,'LowerThreshold') ..
                ' UpperThreshold=' .. attr(item,'UpperThreshold'))
        end
    end
end
local function keyName(map)
    local ok, s = pcall(function() return map.Key.KeyName:ToString() end)
    return ok and tostring(s) or '<unknown>'
end
local function scan()
    local pawn
    for _, candidate in ipairs(FindAllOf('BP_Tadpole_C') or {}) do
        if valid(candidate) then
            local ok, piloted = pcall(function()
                return candidate:IsLocallyControlled() and candidate:IsPiloted()
            end)
            if ok and piloted then pawn = candidate; break end
        end
    end
    if not pawn then return false end
    local pc = pawn:GetController()
    if not valid(pc) then return false end
    emit('Local piloted pawn=' .. full(pawn))
    emit('Player controller=' .. full(pc))
    local localPlayer
    pcall(function() localPlayer = pc.Player end)
    emit('LocalPlayer=' .. full(localPlayer))

    local subs = FindAllOf('EnhancedInputLocalPlayerSubsystem') or {}
    for _, sub in ipairs(subs) do
        local outer
        pcall(function() outer = sub:GetOuter() end)
        if valid(localPlayer) and valid(outer) and
           localPlayer:GetAddress() == outer:GetAddress() then
            emit('Matched player subsystem=' .. full(sub))
            local functionNames = {}
            local ok, why = pcall(function()
                sub:GetClass():ForEachFunction(function(fn)
                    local name = fn:GetFName():ToString()
                    if name:find('Mapping') or name:find('Context') or name:find('Input') then
                        functionNames[#functionNames + 1] = name
                    end
                end)
            end)
            emit('Subsystem reflected method scan=' .. (ok and 'ok' or ('unavailable: ' .. tostring(why))))
            if ok then emit('Input-related functions=' .. table.concat(functionNames, ', ')) end
            -- Do not call HasMappingContext: its reflected signature has an out-priority parameter.
        end
    end

    local found = 0
    for _, context in ipairs(FindAllOf('InputMappingContext') or {}) do
        local name = full(context)
        if name:find('/IMC_Tadpole', 1, true) then
            found = found + 1
            emit('CONTEXT (loaded, NOT confirmed active): ' .. name)
            local array = arrayProp(context, 'Mappings')
            if not array then array = arrayProp(context, 'DefaultKeyMappings') end
            if not array then emit('mapping array inaccessible')
            else
                local n = arrayCount(array)
                emit('mappings=' .. n)
                local matches = 0
                for j = 1, math.min(n, 200) do
                    local ok, map = pcall(function() return array[j] end)
                    if ok then
                        local key = keyName(map)
                        if key == 'Gamepad_Right2D' or key == 'Gamepad_Left2D' or
                           key == 'Gamepad_RightX' or key == 'Gamepad_RightY' then
                            matches = matches + 1
                            local action
                            pcall(function() action = map.Action end)
                            emit(string.format('MAP #%d key=%s action=%s',j,key,full(action)))
                            mods(map, 'Modifiers', 'mapping#' .. j)
                            mods(map, 'Triggers', 'mapping#' .. j)
                            if valid(action) then
                                emit('Action ValueType=' .. attr(action,'ValueType') ..
                                    ' bConsumeInput=' .. attr(action,'bConsumeInput') ..
                                    ' bTriggerWhenPaused=' .. attr(action,'bTriggerWhenPaused'))
                                mods(action, 'Modifiers', 'action#' .. j)
                                mods(action, 'Triggers', 'action#' .. j)
                            end
                        end
                    end
                end
                emit('Relevant mapping entries=' .. matches)
            end
        end
    end
    if found == 0 then
        if not warned then emit('No loaded Tadpole contexts yet; retrying'); warned = true end
        return false
    end
    emit('COMPLETE: READ ONLY. Context presence does not establish activation.')
    return true
end

emit('Loaded v2; waiting for locally piloted Tadpole.')
LoopAsync(2000, function()
    ticks = ticks + 1
    if not finished then
        ExecuteInGameThread(function()
            if finished then return end
            local ok, result = pcall(scan)
            if ok and result then finished = true
            elseif not ok then emit('Inspection error (no game state changed): ' .. tostring(result)) end
        end)
    end
    if ticks >= 90 and not finished then emit('Probe timeout; see UE4SS log') end
    return finished or ticks >= 90
end)
