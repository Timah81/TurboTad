-- Game-side keyboard contribution for T6D8. No raw key logs or global hooks.
local M={}
local allowed={SpaceBar=true,LeftShift=true,LeftControl=true,RightShift=true,
    RightControl=true,LeftAlt=true,RightAlt=true,Tab=true,Enter=true,BackSpace=true,
    Escape=true,Insert=true,Delete=true,Home=true,End=true,PageUp=true,PageDown=true,
    Up=true,Down=true,Left=true,Right=true,CapsLock=true,NumLock=true,ScrollLock=true,
    Semicolon=true,Equals=true,Comma=true,Hyphen=true,Period=true,Slash=true,
    Tilde=true,LeftBracket=true,Backslash=true,RightBracket=true,Apostrophe=true,
    Multiply=true,Add=true,Subtract=true,Decimal=true,Divide=true}
for i=65,90 do allowed[string.char(i)]=true end
local digits={'Zero','One','Two','Three','Four','Five','Six','Seven','Eight','Nine'}
for i,n in ipairs(digits) do allowed[n]=true;allowed['NumPad'..n]=true end
for i=1,12 do allowed['F'..i]=true end
function M.validName(name) return name=='-' or allowed[name]==true end

function M.new(makeKey)
    local self={keys={},armed={},held={},signature=nil,owner=nil}
    function self:reset()
        self.armed={};self.held={};self.signature=nil;self.owner=nil;self.keys={}
    end
    -- `base` is the already-inverted non-keyboard contribution. Keyboard
    -- directions are serialized in final negative/positive orientation.
    function self:resolve(frame,base,pc,owner,accepting)
        if not frame or not frame.keyboard then self:reset();return base,frame and frame.boost or 0 end
        if not accepting or not base or not pc then self:reset();return base and {0,0,0,0,0,0} or nil,0 end
        local names=frame.keyboard
        local sig=table.concat(names,' ')
        if self.signature~=sig or self.owner~=owner then
            self:reset();self.signature=sig;self.owner=owner
        end
        local down={}
        for _,name in ipairs(names) do
            if name~='-' and down[name]==nil then
                if not self.keys[name] then self.keys[name]=makeKey(name) end
                local raw=pc:IsInputKeyDown(self.keys[name])
                if type(raw)~='boolean' and raw~=0 and raw~=1 then error('Unexpected keyboard state type') end
                local pressed=raw==true or raw==1
                if not pressed then self.armed[name]=true end
                down[name]=pressed and self.armed[name]==true
            end
        end
        local axes={}
        for i=1,6 do
            local negative=down[names[2*i-1]] and 1 or 0
            local positive=down[names[2*i]] and 1 or 0
            axes[i]=math.max(-1,math.min(1,base[i]+positive-negative))
        end
        local key=names[13]
        local token=frame.boost
        if key~='-' then
            local pressed=down[key]==true
            token=pressed and not self.held[key] and frame.seq or 0
            self.held[key]=pressed
        end
        return axes,token
    end
    return self
end
return M
