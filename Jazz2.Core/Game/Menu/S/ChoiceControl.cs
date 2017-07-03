﻿using Duality;
using Duality.Drawing;
using Duality.Input;

namespace Jazz2.Game.Menu.S
{
    public class ChoiceControl : MenuControlBase
    {
        private string title;
        private string[] choices;
        private int selectedIndex;

        public override bool IsEnabled => true;

        public override bool IsInputCaptured => false;

        public int SelectedIndex => selectedIndex;

        public ChoiceControl(MainMenu api, string title, int selectedIndex, params string[] choices) : base(api)
        {
            this.title = title;
            this.choices = choices;
            this.selectedIndex = selectedIndex;
        }

        public override void OnDraw(IDrawDevice device, Canvas c, ref Vector2 pos, bool focused)
        {
            int charOffset = 0;

            if (focused) {
                float size = 0.5f + /*MainMenu.EaseOutElastic(animation) **/ 0.6f;

                api.DrawStringShadow(device, ref charOffset, title, pos.X, pos.Y,
                    Alignment.Center, null, size, 0.7f, 1.1f, 1.1f, charSpacing: 0.9f);
            } else {
                api.DrawString(device, ref charOffset, title, pos.X, pos.Y, Alignment.Center,
                    ColorRgba.TransparentBlack, 0.9f);
            }

            for (int i = 0; i < choices.Length; i++) {
                if (selectedIndex == i) {
                    api.DrawStringShadow(device, ref charOffset, choices[i], pos.X + (i - 1) * 100f, pos.Y + 28f, Alignment.Center,
                        null, 0.9f, 0.4f, 0.55f, 0.55f, 8f, 0.9f);
                } else {
                    api.DrawString(device, ref charOffset, choices[i], pos.X + (i - 1) * 100f, pos.Y + 28f, Alignment.Center,
                        ColorRgba.TransparentBlack, 0.8f, charSpacing: 0.9f);
                }
            }

            api.DrawStringShadow(device, ref charOffset, "<", pos.X - (100f + 40f), pos.Y + 28f, Alignment.Center,
                ColorRgba.TransparentBlack, 0.7f);
            api.DrawStringShadow(device, ref charOffset, ">", pos.X + (100f + 40f), pos.Y + 28f, Alignment.Center,
                ColorRgba.TransparentBlack, 0.7f);

            pos.Y += 70f;
        }

        public override void OnUpdate()
        {
            if (DualityApp.Keyboard.KeyHit(Key.Left)) {
                if (selectedIndex > 0) {
                    selectedIndex--;
                }
            } else if (DualityApp.Keyboard.KeyHit(Key.Right)) {
                if (selectedIndex < choices.Length - 1) {
                    selectedIndex++;
                }
            }
        }
    }
}