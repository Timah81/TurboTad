-- TEMPORARY diagnostic probe for the game-side keyboard input path.
-- Remove this file and its call from main.lua before any release build.
local M = {}
local keyW
local lastDown
local errorReported = false

function M.tick(pawn)
    if not keyW then
        local ok, value = pcall(function()
            return { KeyName = FName("W") }
        end)
        if not ok then
            if not errorReported then
                print("[TurboTad][KeyboardInputProbe] Could not construct FKey table: " .. tostring(value))
                errorReported = true
            end
            return
        end
        keyW = value
    end

    local ok, down = pcall(function()
        local pc = pawn:GetController()
        if not pc then return false end
        return pc:IsInputKeyDown(keyW)
    end)
    if not ok then
        if not errorReported then
            print("[TurboTad][KeyboardInputProbe] IsInputKeyDown call failed: " .. tostring(down))
            errorReported = true
        end
        return
    end

    down = down == true or down == 1
    if down ~= lastDown then
        print("[TurboTad][KeyboardInputProbe] W held = " .. tostring(down))
        lastDown = down
    end
end

return M
