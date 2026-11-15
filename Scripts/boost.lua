local M={}
function M.new(trigger)
    local self={seen=0,nextAt=0,error=nil,lastRequested=0,duration=0,clockValue=0}
    function self:sync(token) self.seen=token or 0 end
    function self:time(real,wall,world)
        local native=type(real)=='number' and real==real and real>=0 and real<math.huge
        local source=native and 'real' or 'wall'
        local sample=native and real or wall
        if self.clockSource==source and (not native or self.clockWorld==world)
            and self.clockSample and sample>=self.clockSample then
            self.clockValue=self.clockValue+sample-self.clockSample
        end
        self.clockSource=source;self.clockSample=sample;self.clockWorld=world
        return self.clockValue
    end
    function self:remaining(now) return math.max(0,math.ceil(self.nextAt-now)) end
    function self:progress(now)
        if self.duration<=0 then return 1 end
        return math.max(0,math.min(1,1-(self.nextAt-now)/self.duration))
    end
    function self:request(pawn,token,cooldown,now,axes,settings)
        if not token or token<=0 or token==self.seen then return false end
        self.seen=token
        if now<self.nextAt then return false end
        local ok,result,reason=pcall(trigger,pawn,axes,settings)
        if not ok or not result then
            self.error=tostring(ok and (reason or 'Native ring boost unavailable') or result)
            return false
        end
        self.error=nil
        self.lastRequested=token
        self.duration=cooldown
        self.nextAt=now+cooldown
        return true
    end
    return self
end
return M
