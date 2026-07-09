using UnityEngine;

namespace AccentGuesser.App
{
    /// <summary>
    /// Single source of truth for the stage geometry: where players sit, where the announcer
    /// stands, and where the cameras go. Liar's-Bar-style staging — the four players sit in a
    /// shallow arc on ONE side of the table, shoulder to shoulder, all facing the penguin
    /// announcer behind the bar across the table.
    ///
    /// Shared by <see cref="TavernPresenter"/> (first-person camera at the LOCAL seat — each
    /// multiplayer client passes its own seat) and by the editor scene builder (stool/avatar
    /// placement + the baked selection camera), so layout and camera can never drift apart.
    /// </summary>
    public static class TavernSeating
    {
        /// <summary>
        /// Where the announcer stands: behind the bar counter (+X wall, the one with the bottle
        /// shelves), like a bartender-host facing the room.
        /// </summary>
        public static readonly Vector3 AnnouncerPos = new Vector3(3.6f, 0f, 0f);

        /// <summary>Announcer head height — where seated players' gazes rest.</summary>
        public const float AnnouncerHeadHeight = 1.5f;

        /// <summary>Arc radius from the table centre for the player row (-X side, backs to the fireplace).</summary>
        public const float SeatRadius = 2.6f;

        /// <summary>
        /// Player arc angles in degrees around the table centre (180° = -X, the fireplace side,
        /// opposite the bar). Shoulder-to-shoulder row: Bulldog, Giraffe, Horse, Fox.
        /// </summary>
        public static readonly float[] SeatAngles = { 144f, 168f, 192f, 216f };

        /// <summary>
        /// Per-AVATAR first-person eye heights, matched to each animal's actual head height
        /// (bulldog 1.88m, giraffe 2.26m, horse 1.82m, fox 1.93m, cat 1.99m — the giraffe
        /// really does look down on everyone). Indexed by avatar id, not seat: avatars are
        /// chosen, so any animal can sit anywhere.
        /// </summary>
        public static readonly float[] AvatarEyeHeights = { 1.65f, 2.00f, 1.60f, 1.70f, 1.75f };

        /// <summary>Camera sits this far in front of the avatar's face so the head never clips
        /// and your own cheeks/ears stay out of the frame edges.</summary>
        public const float EyeForward = 0.38f;

        /// <summary>First-person field of view.</summary>
        public const float FieldOfView = 64f;

        public static int SeatCount => SeatAngles.Length;

        /// <summary>Where a seat's avatar stands (feet position).</summary>
        public static Vector3 SeatPosition(int seat)
        {
            float rad = SeatAngles[Mathf.Clamp(seat, 0, SeatAngles.Length - 1)] * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * SeatRadius;
        }

        /// <summary>Flat unit direction a seated avatar faces: at the announcer across the table.</summary>
        public static Vector3 SeatFacing(int seat)
        {
            Vector3 dir = AnnouncerPos - SeatPosition(seat);
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-4f ? Vector3.forward : dir.normalized;
        }

        /// <summary>
        /// First-person camera pose for a seat: just in front of the seated ANIMAL's face at its
        /// eye height, gaze resting on the announcer. Mouse-look yaw/pitch is applied on top of
        /// this base rotation. (The local body itself is hidden by the stage — at these offsets
        /// any sideways glance would otherwise fill the view with your own head.)
        /// </summary>
        public static void FirstPersonPose(int seat, int avatar, out Vector3 position, out Quaternion rotation)
        {
            int i = Mathf.Clamp(seat, 0, SeatAngles.Length - 1);
            int a = Mathf.Clamp(avatar, 0, AvatarEyeHeights.Length - 1);
            Vector3 facing = SeatFacing(i);
            position = SeatPosition(i) + facing * EyeForward + Vector3.up * AvatarEyeHeights[a];
            Vector3 lookAt = AnnouncerPos + Vector3.up * AnnouncerHeadHeight;
            rotation = Quaternion.LookRotation((lookAt - position).normalized);
        }

        /// <summary>Seat pose with a mid-range default eye height (when the avatar is unknown).</summary>
        public static void FirstPersonPose(int seat, out Vector3 position, out Quaternion rotation) =>
            FirstPersonPose(seat, 0, out position, out rotation);

        /// <summary>
        /// The avatar-selection vantage: standing at the bar beside the announcer, sizing up the
        /// row of four animals — they all face the camera while you choose who to be.
        /// </summary>
        public static void SelectionPose(out Vector3 position, out Quaternion rotation)
        {
            position = new Vector3(2.4f, 1.9f, 0f);
            rotation = Quaternion.LookRotation(
                (new Vector3(-2.5f, 1.0f, 0f) - position).normalized);
        }

        /// <summary>Field of view for the selection view.</summary>
        public const float SelectionFieldOfView = 60f;
    }
}
