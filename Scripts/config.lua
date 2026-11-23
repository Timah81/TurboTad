return {
    RotationInputScale=1.0,
    -- Applied only while TurboTad owns an eligible piloted Tadpole; restored on release.
    TadpoleMaxSwimAcceleration=6000, -- rollback fallback when dynamic scaling is off
    TadpoleDynamicSwimAcceleration=true, -- calibrated approximation; test branch only
    InputTimeoutSeconds=0.25,
    EnableBoostHud=true,
    VelocityBoostCmPerSecond=4200,
}
