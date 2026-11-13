namespace Forge.Training
{
    /// <summary>
    /// How to do each move, in a line you can read from the floor (user, 2026-11-13): shown under
    /// the exercise in the workout screen when the program has no note of its own, and as the
    /// instructions of the guided rest-day flow. Short imperatives, form first. Every exercise a
    /// shipped quest uses must have one (ProgramTests).
    /// </summary>
    public static class ExerciseCues
    {
        private static readonly Dictionary<string, string> Cues = new(StringComparer.OrdinalIgnoreCase)
        {
            // Mobility and warm-up
            ["Cat-Cow"] = "On hands and knees: round your back to the ceiling, then let your belly sink and look up. Move with your breath.",
            ["World's Greatest Stretch"] = "Lunge forward, hand inside the front foot, then twist and reach that arm to the sky.",
            ["90/90 Hip Switch"] = "Sit with both knees bent at 90°, then swing both knees to the other side. Stay tall.",
            ["Deep Squat Hold"] = "Sink into your deepest squat, heels down, chest up. Push your knees out with your elbows.",
            ["Child's Pose"] = "Kneel, sit back on your heels and walk your hands forward. Rest your forehead and breathe.",
            ["Downward Dog"] = "Hands and feet down, hips high. Press the floor away and let your heels sink.",
            ["Thread the Needle"] = "On all fours, slide one arm under your body and rest that shoulder down. Twist, then switch.",
            ["Lat Wall Slide"] = "Back and arms flat on the wall, slide your arms up overhead and down without arching.",
            ["Air Squat"] = "Feet shoulder-width, sit your hips back and down, chest up, then stand tall.",
            ["Glute Bridge"] = "Lie on your back, knees bent. Drive through your heels and lift your hips; squeeze at the top.",
            ["Kettlebell Halo"] = "Hold the bell upside down by the horns and circle it slowly around your head. Keep your ribs down.",
            ["Dead Hang"] = "Hang from the bar with straight arms, shoulders gently pulled down. Breathe and relax your grip.",

            // Squat and lunge
            ["Goblet Squat"] = "Hold the bell at your chest, sit down between your heels, elbows inside your knees, stand tall.",
            ["Heel-Elevated Goblet Squat"] = "Heels raised, weight at your chest. Sit straight down, knees forward over your toes.",
            ["Sumo Goblet Squat"] = "Wide stance, toes out, weight hanging between your legs. Sit down, then stand and squeeze.",
            ["Kettlebell Front Squat"] = "Bell racked at your shoulder, squat down with your chest proud. Don't let the bell pull you forward.",
            ["Split Squat"] = "Long stance, back heel up. Drop your back knee toward the floor, then drive up through the front foot.",
            ["Bulgarian Split Squat"] = "Back foot on the block, front foot far out. Lower straight down, front knee tracking your toes.",
            ["Walking Lunge"] = "Step forward into a lunge, back knee near the floor, then step through into the next one.",
            ["Racked Reverse Lunge"] = "Bell racked at your shoulder. Step back into a lunge, then drive up through the front heel.",
            ["Box Step-up"] = "Whole foot on the box, press up through that heel to stand tall, then step down with control.",

            // Hinge and glutes
            ["Romanian Deadlift"] = "Soft knees, push your hips back and slide the weights down your legs. Flat back, then stand.",
            ["Staggered-Stance Romanian Deadlift"] = "Back foot on its toes for balance. Hinge over the front leg, flat back, then stand.",
            ["Kettlebell Deadlift"] = "Bell between your feet. Hips back, flat back, grab and stand tall by driving through the floor.",
            ["Kettlebell Swing"] = "Hike the bell back, then snap your hips forward to float it to chest height. Arms just guide it.",
            ["Kettlebell Clean"] = "Swing the bell up close to your body and let it land softly in the rack at your shoulder.",
            ["Hip Thrust"] = "Upper back on the box, weight on your hips. Drive your hips up until level, squeeze, lower.",
            ["Feet-Elevated Hamstring Bridge"] = "Heels on the block, knees slightly bent. Lift your hips and hold them level.",
            ["Turkish Get-up"] = "Bell locked out above you, eyes on it. Rise to standing step by step, then reverse it slowly.",

            // Push
            ["Push-up"] = "Hands under your shoulders, body in one straight line. Lower your chest to the floor and press away.",
            ["Deficit Push-up"] = "Hands raised on blocks so your chest sinks lower. Rigid body, lower slowly, press up.",
            ["Dumbbell Floor Press"] = "Lie on the floor, elbows at 45°. Lower until your elbows touch, then press straight up.",
            ["Kettlebell Floor Press"] = "Lie down, bell in one hand, elbow on the floor. Press straight up and lower with control.",
            ["Dumbbell Overhead Press"] = "Weights at your shoulders, ribs down. Press straight overhead, then lower to your shoulders.",
            ["Arnold Press"] = "Start palms facing you, then rotate them forward as you press overhead. Reverse on the way down.",
            ["Kettlebell Press"] = "Bell racked, squeeze your glutes. Press it straight overhead, then pull it back down to the rack.",
            ["Bench Dip"] = "Hands on the edge behind you, legs out. Bend your elbows to lower, then press back up.",

            // Pull
            ["One-arm Row"] = "Free hand on the block, flat back. Pull the weight to your hip, then lower all the way.",
            ["Bent-over Row"] = "Hinge forward with a flat back. Pull both weights to your ribs, squeeze, then lower.",
            ["Kettlebell Row"] = "Free hand on your knee, flat back. Row the bell to your hip, then lower it with control.",
            ["Renegade Row"] = "Plank on the dumbbells, feet wide. Row one weight to your hip without twisting, then switch.",
            ["Dumbbell Pullover"] = "Lie on the mat, weight held over your chest. Lower it behind your head with soft elbows, then back.",
            ["Pull-up Negative"] = "Jump or step to the top of the bar, then lower yourself as slowly as you can.",

            // Shoulders and arms
            ["Lateral Raise"] = "Slight bend in the elbows, raise the weights out to shoulder height, then lower slowly.",
            ["Rear Delt Fly"] = "Hinge forward, arms hanging. Open your arms wide like wings, squeezing your shoulder blades.",
            ["Dumbbell Curl"] = "Elbows pinned to your sides, curl the weights up, then lower all the way down.",
            ["Hammer Curl"] = "Palms facing each other, elbows still. Curl up, then lower with control.",
            ["Dumbbell Triceps Extension"] = "Weight overhead in both hands, elbows pointing up. Lower it behind your head, then extend.",
            ["Dumbbell Skull Crusher"] = "Lie down, arms straight up. Bend only your elbows to lower the weights by your head, then extend.",
            ["Dumbbell Calf Raise"] = "Weights at your sides, rise high onto your toes, pause, then lower slowly.",
            ["Single-Leg Calf Raise"] = "Ball of one foot on the block's edge. Rise all the way up, then lower your heel below the edge.",

            // Core
            ["Plank"] = "Forearms down, body in one straight line. Squeeze your glutes and breathe.",
            ["Side Plank"] = "On one forearm, feet stacked. Lift your hips into a straight line and hold.",
            ["Hollow Hold"] = "On your back, low back pressed down. Lift your shoulders and legs and hold the banana shape.",
            ["Dead Bug"] = "On your back, arms and knees up. Lower the opposite arm and leg, low back glued down, switch.",
            ["Reverse Crunch"] = "On your back, knees bent. Curl your hips off the floor toward your chest, then lower slowly.",

            // Conditioning
            ["Jumping Jacks"] = "Jump your feet wide as your arms go overhead, then back together. Stay light on your feet.",
            ["High Knees"] = "Run in place, driving your knees up to hip height. Pump your arms.",
            ["Mountain Climbers"] = "Plank on your hands, drive your knees to your chest one at a time, fast.",
            ["Burpee"] = "Squat, hands down, jump your feet back, then forward, and jump up with your arms overhead.",
            ["Squat Jumps"] = "Squat down, then jump as high as you can. Land softly and sink straight into the next one.",
            ["Shadow Boxing"] = "Light on your feet, hands up. Throw quick straight punches and hooks, and keep moving.",
            ["Farmer's Carry"] = "Heavy weight in each hand, stand tall, and walk with short steady steps.",

            // Bands
            ["Banded Lateral Walk"] = "Band around your legs, in a half squat. Step sideways, keeping tension on the band.",
            ["Banded Clamshell"] = "Lie on your side, knees bent, band above them. Open your top knee like a clam, then close.",
        };

        /// <summary>The cue for an exercise, or null if it has none yet.</summary>
        public static string? For(string exercise) => Cues.GetValueOrDefault(exercise);

        public static bool Has(string exercise) => Cues.ContainsKey(exercise);
    }
}
