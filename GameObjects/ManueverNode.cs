using SpaceTrafficController.Simulation;
using SpaceTrafficController.UI;
using SpaceTrafficController.Utilities;
using System;
using System.Numerics;

namespace SpaceTrafficController.GameObjects;


public class ManeuverNode
{
    Orbit Orbit { get; set; }

    public ManeuverNode(Orbit orbit)
    {
        Orbit = orbit;
    }

    public double TrueAnomaly { get; set; }
    public Vector2 ScreenPosition { get; set; }

    public float ProgradeDeltaV { get; set; } = 0f;
    public float NormalDeltaV { get; set; } = 0f;
    public double NodeTime { get => Orbit.TimeToTrueAomaly(TrueAnomaly); }
    public bool IsConfirmed { get; set; } = false;
    public bool IsDragged { get; set; } = false;
    public Orbit PredictedOrbit
    {
        get
        {
            if (ProgradeDeltaV == 0 && NormalDeltaV == 0)
                return null;
            var velocity = Orbit.GetVelocityAtAngle(TrueAnomaly);
            var position = Orbit.GetPositionAtAngle(TrueAnomaly);

            var prograde = Vector2.Normalize(velocity);
            var normal = new Vector2(-prograde.Y, prograde.X);

            var deltaV = prograde * ProgradeDeltaV + normal * NormalDeltaV;
            var newVelocity = velocity + deltaV;

            return OrbitUtils.GetOrbitFromStateVectors(position, newVelocity);
        }
    }

    public float ButtonOffset { get; set; } = 1;
    public float ButtonThickness { get; set; } = 1;
    public float ButtonRadius { get; set; } = 1;
    public Vector2 DragOffset { get; set; } = new Vector2(0, 0);
    public ManeuverDragType DragType { get; set; }
    public Vector2 VelocityDir { get => Vector2.Normalize(Orbit.GetVelocityAtAngle(TrueAnomaly)); }
    public Vector2 NormalDir { get => new Vector2(-VelocityDir.Y, VelocityDir.X); }

    public Button ProgradeButton 
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Prograde)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Prograde));
            return new()
            {
                Position = ScreenPosition + VelocityDir * offset,
                Label = ButtonLabel.Plus,
                Color = Microsoft.Xna.Framework.Color.GreenYellow,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button RetrogradeButton 
    { 
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Retrograde)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Retrograde));
            return new()
            {
                Position = ScreenPosition - VelocityDir * offset,
                Label = ButtonLabel.Minus,
                Color = Microsoft.Xna.Framework.Color.GreenYellow,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button NormalButton
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Normal)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Normal));
            return new()
            {
                Position = ScreenPosition - NormalDir * offset,
                Label = ButtonLabel.Plus,
                Color = Microsoft.Xna.Framework.Color.LightBlue,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button AntinormalButton 
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Antinormal)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Antinormal));
            return new()
            {
                Position = ScreenPosition + NormalDir * offset,
                Label = ButtonLabel.Minus,
                Color = Microsoft.Xna.Framework.Color.AliceBlue,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button ConfirmButton 
    { 
        get => new() 
        {
            Position = ScreenPosition - (-VelocityDir + NormalDir) * (1 / MathF.Sqrt(2)) * ButtonOffset * MathF.Sqrt(2), 
            Label = ButtonLabel.V, 
            Color = Microsoft.Xna.Framework.Color.DarkGreen, 
            Thickness = ButtonThickness, 
            Radius = ButtonRadius 
        }; 
    }
    public Button CancelButton 
    { 
        get => new() 
        { 
            Position = ScreenPosition - (VelocityDir + NormalDir) * (1 / MathF.Sqrt(2)) * ButtonOffset * MathF.Sqrt(2), 
            Label = ButtonLabel.Minus, 
            Color = Microsoft.Xna.Framework.Color.DarkRed, 
            Thickness = ButtonThickness, 
            Radius = ButtonRadius
        }; 
    }
    public Vector2 GetDirectionVectorForDrag(ManeuverDragType maneuverDragType)
    {
        return maneuverDragType switch
        {
            ManeuverDragType.Prograde => VelocityDir,
            ManeuverDragType.Retrograde => -VelocityDir,
            ManeuverDragType.Normal => -NormalDir,
            ManeuverDragType.Antinormal => NormalDir,
            _ => Vector2.Zero,
        };
    }
}

public enum ManeuverDragType { None, Prograde, Retrograde, Normal, Antinormal }
