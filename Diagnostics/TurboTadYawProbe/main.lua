-- TurboTad right-stick yaw context discovery; standalone, READ ONLY.
-- Does not call MapKey, UnmapKey, AddMappingContext or RemoveMappingContext.
local SCRIPT = debug.getinfo(1, 'S').source:gsub('^@', '')
local DIR = SCRIPT:match('^(.*[/\\])') or ''
local LOG = DIR .. '..\\TurboTadYawProbe.log'
local attempts, done, waitingReported = 0, false, false

local function out(message)
    local line = '[TurboTadYawProbe] ' .. tostring(message)
    print(line .. '\n')
    pcall(function()
        local f = io.open(LOG, 'a')
        if f then f:write(os.date('!%Y-%m-%dT%H:%M:%SZ '), line, '\n'); f:close() end
    end)
end

local function valid(obj)
    local ok, result = pcall(function() return obj ~= nil and obj:IsValid() end)
    return ok and result
end
local function full(obj)
    if not valid(obj) then return '<unavailable>' end
    local ok, name = pcall(function() return obj:GetFullName() end)
    return ok and tostring(name) or '<name unavailable>'
end
local function address(obj)
    local ok, value = pcall(function() return valid(obj) and obj:GetAddress() end)
    return ok and value or nil
end
local function keyName(mapping)
    local ok, name = pcall(function() return mapping.Key.KeyName:ToString() end)
    if ok then return tostring(name) end
    ok, name = pcall(function() return tostring(mapping.Key.KeyName) end)
    return ok and name or '<key inaccessible>'
end
local function dump()
    local pawn = FindFirstOf('BP_Tadpole_C')
    if not valid(pawn) then return false end
    local ok, pilot = pcall(function()
        return pawn:IsLocallyControlled() and pawn:IsPiloted()
    end)
    if not ok or not pilot then return false end

    local pc = pawn:GetController()
    if not valid(pc) then return false end
    local contexts = FindAllOf('InputMappingContext') or {}
    local matches = {}
    for _, context in ipairs(contexts) do
        local n = full(context)
        if n:find('IMC_Tadpole', 1, true) then matches[#matches + 1] = context end
    end
    if #matches == 0 then
        if not waitingReported then
            out('Local Tadpole piloted; IMC_Tadpole not found among loaded InputMappingContext objects yet')
            waitingReported = true
        end
        return false
    end

    out('Local Tadpole piloted. PlayerController=' .. full(pc))
    local localPlayer
    pcall(function() localPlayer = pc.Player end)
    out('PlayerController.Player=' .. full(localPlayer))

    local subsystems = FindAllOf('EnhancedInputLocalPlayerSubsystem') or {}
    out('EnhancedInputLocalPlayerSubsystem instances=' .. tostring(#subsystems))
    for i, subsystem in ipairs(subsystems) do
        local parent
        pcall(function() parent = subsystem:GetOuter() end)
        local same = address(localPlayer) ~= nil and address(localPlayer) == address(parent)
        out(string.format('Subsystem %d: %s; outer=%s; matches local player=%s',
            i, full(subsystem), full(parent), tostring(same)))
        for _, context in ipairs(matches) do
            local success, has = pcall(function() return subsystem:HasMappingContext(context) end)
            out('  HasMappingContext(' .. full(context) .. ') = ' ..
                (success and tostring(has) or 'method unavailable'))
        end
    end

    for _, context in ipairs(matches) do
        out('CONTEXT ' .. full(context))
        local mappings
        local property
        local readable, value = pcall(function() return context.Mappings end)
        if readable and value ~= nil then mappings = value; property = 'Mappings' end
        if not mappings then
            readable, value = pcall(function() return context.DefaultKeyMappings end)
            if readable and value ~= nil then mappings = value; property = 'DefaultKeyMappings' end
        end
        if not mappings then
            out('Mappings inaccessible through reflected properties; no changes attempted')
        else
            local counted, count = pcall(function() return #mappings end)
            if not counted then
                out(property .. ' array length inaccessible; no changes attempted')
            else
                out(property .. ' entries=' .. tostring(count))
                for i = 1, math.min(count, 150) do
                    local success, label = pcall(function()
                        local map = mappings[i]
                        return string.format('  %d: key=%s action=%s', i,
                            keyName(map), full(map.Action))
                    end)
                    out(success and label or ('  ' .. i .. ': entry inaccessible'))
                end
                if count > 150 then out('Additional mappings omitted for bounded output') end
            end
        end
    end
    out('Probe finished. READ ONLY; no native mapping changed.')
    return true
end

out('Loaded; waiting for local player to pilot Tadpole. One-shot read-only inspection.')
LoopAsync(2000, function()
    attempts = attempts + 1
    if not done then
        ExecuteInGameThread(function()
            if done then return end
            local ok, result = pcall(dump)
            if ok and result then done = true
            elseif not ok then out('Probe exception (no mapping changed): ' .. tostring(result)) end
        end)
    end
    if attempts >= 90 and not done then
        out('Timed out without a complete context dump. Remove this probe before normal play.')
    end
    return done or attempts >= 90
end)
