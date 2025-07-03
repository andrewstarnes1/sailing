using System.Collections.Generic;
using UnityEngine;

namespace KWS
{

    internal class KWS_DynamicWavesHelpers
    {
        public struct FoamParticle
        {
            Vector3 position;
            float   initialRandom01;

            Vector3 prevPosition;
            float   prevLifetime;

            Vector3 velocity;
            float   currentLifetime;

            Vector4 color;
            Vector4 prevColor;

            float           isFreeMoving;
            float           shorelineMask;
            private Vector2 _pad;
        }

        public struct SplashParticle
        {
            float   initialRandom01;
            Vector3 position;

            Vector3 velocity;
            float   currentLifetime;

            uint  particleType;
            float distanceToSurface;
            float uvOffset;
            float initialSpeed;

            Vector3 prevPosition;
            float   prevLifetime;

        }


        private static Mesh _triangleMesh;



       
      

        public static void Release()
        {
            KW_Extensions.SafeDestroy(_triangleMesh);
        }
    }
}