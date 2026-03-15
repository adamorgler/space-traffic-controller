using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceTrafficController.Simulation.OrbitingObjects;

public abstract class HasOrbit
{
    public HasOrbit(Orbit orbit)
    {
        Orbit = orbit;
    }

    public Orbit Orbit { get; set; }

    public void Update(double timeStep)
    {
        // record previous anomaly so extensions can detect crossings
        Orbit.PreviousTrueAnomaly = Orbit.TrueAnomaly;
        Orbit.Update(timeStep);
        UpdateExtension(timeStep);
    }

    public abstract void UpdateExtension(double timeStep);
}
