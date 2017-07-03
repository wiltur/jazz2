﻿namespace Jazz2.Actors
{
    public class PlayerCorpse : ActorBase
    {
        public override void OnAttach(ActorInstantiationDetails details)
        {
            base.OnAttach(details);

            PlayerType playerType = (PlayerType)details.Params[0];
            isFacingLeft = (details.Params[1] != 0);

            switch (playerType) {
                case PlayerType.Jazz:
                    RequestMetadata("Interactive/PlayerJazz");
                    break;
                case PlayerType.Spaz:
                    RequestMetadata("Interactive/PlayerSpaz");
                    break;
                case PlayerType.Lori:
                    RequestMetadata("Interactive/PlayerLori");
                    break;
            }

            SetAnimation("CORPSE");
            RefreshFlipMode();

            collisionFlags = CollisionFlags.None;
        }

        protected override void OnUpdate()
        {
            // Nothing to do...
        }
    }
}