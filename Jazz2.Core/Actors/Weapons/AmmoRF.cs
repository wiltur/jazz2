﻿using Duality;
using Jazz2.Game;
using Jazz2.Game.Structs;

namespace Jazz2.Actors.Weapons
{
    public class AmmoRF : AmmoBase
    {
        private Vector2 gunspotPos;
        private bool fired;

        private float smokeTimer = 3f;

        public override WeaponType WeaponType => WeaponType.RF;

        public override void OnAttach(ActorInstantiationDetails details)
        {
            base.OnAttach(details);

            strength = 2;
            collisionFlags &= ~CollisionFlags.ApplyGravitation;

            RequestMetadata("Weapon/RF");

            LightEmitter light = AddComponent<LightEmitter>();
            light.Intensity = 0.8f;
            light.Brightness = 0.8f;
            light.RadiusNear = 3f;
            light.RadiusFar = 12f;
        }

        public void OnFire(Player owner, Vector3 gunspotPos, Vector3 speed, float angle, bool isFacingLeft, byte upgrades)
        {
            base.owner = owner;
            base.IsFacingLeft = isFacingLeft;
            base.upgrades = upgrades;

            this.gunspotPos = gunspotPos.Xy;

            float angleRel = angle * (isFacingLeft ? -1 : 1);

            const float baseSpeed = 2.6f;
            if (isFacingLeft) {
                speedX = MathF.Min(0, speed.X) - MathF.Cos(angleRel) * baseSpeed;
            } else {
                speedX = MathF.Max(0, speed.X) + MathF.Cos(angleRel) * baseSpeed;
            }
            speedY = MathF.Sin(angleRel) * baseSpeed;
            speedY += MathF.Abs(speed.Y) * speedY;

            AnimState state = AnimState.Idle;
            if ((upgrades & 0x1) != 0) {
                timeLeft = 35;
                state |= (AnimState)1;
            } else {
                timeLeft = 30;
            }

            Transform.Angle = angle;

            SetAnimation(state);
            PlaySound("Fire", 0.4f);

            renderer.Active = false;
        }

        protected override void OnUpdate()
        {
            float timeMult = Time.TimeMult * 0.5f;

            for (int i = 0; i < 2; i++) {
                TryMovement(timeMult);
                OnUpdateHitbox();
                CheckCollisions(timeMult);
            }

            base.OnUpdate();

            speedX *= 1.06f;
            speedY *= 1.06f;

            if (smokeTimer > 0f) {
                smokeTimer -= Time.TimeMult;
            } else {
                Explosion.Create(api, Transform.Pos, Explosion.TinyBlue);
                smokeTimer = 6f;
            }

            if (!fired) {
                fired = true;

                MoveInstantly(gunspotPos, MoveType.Absolute, true);
                renderer.Active = true;
            }
        }

        protected override bool OnPerish(ActorBase collider)
        {
            Vector3 pos = Transform.Pos;

            foreach (ActorBase collision in api.FindCollisionActorsRadius(pos.X, pos.Y, 36)) {
                Player player = collision as Player;
                if (player != null) {
                    bool pushLeft = (pos.X > player.Transform.Pos.X);
                    player.AddExternalForce(pushLeft ? -4f : 4f, 0f);
                }
            }

            Explosion.Create(api, pos + Speed, Explosion.RF);

            PlaySound("Explode", 0.6f);

            return base.OnPerish(collider);
        }

        protected override void OnHitWallHook()
        {
            DecreaseHealth(int.MaxValue);
        }

        protected override void OnRicochet()
        {
            //base.OnRicochet();

            //Transform.Angle = MathF.Atan2(speedY, speedX);
        }
    }
}