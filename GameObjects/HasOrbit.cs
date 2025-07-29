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
        Orbit.Update(timeStep);
    }
}
