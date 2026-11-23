# Current validation: 19 October 2026

Unity 6000.3.24f1 compiled the revised source and completed **338 workflow/model checks**, followed by **18 focused close-up checks**, in the isolated Validation/ExperienceReviewProject. All passed. Student saves were isolated from the real project.

Reports: **Validation/UsabilityValidation.txt**, **Validation/CloseupValidation.txt**. Logs: **Validation/UsabilityFinal.log**, **Validation/CloseupVerified.log**. Representative rendered images: **Validation/UsabilityScreenshots**. Historical 455-check phase verification remains in AllPhasesVerified.txt; it predates the replacement of the inventory tray.

## Covered

- Study from the introduction and automatically at the start of guided mode; pausing and returning to the bench.
- Real apparatus offered on the table in sequence, without tray controls; correct placements and prerequisite-based assessment offerings.
- Beaker filling inspection, physical target line, removal of the numeric volume card, tap direction and alignment, actual running/stopping water, and agreement between 100 mL and the marked level.
- View joystick movement and pointer ownership in inspection; Bench reset.
- Returning a filled beaker, reopening refill, emptying/refilling and resuming the interrupted preparation action without discarding assembly.
- Side-by-side trial readings, frozen captures, physics calculator loading, the mean calculation and Assign into a saved entry; assessment does not reveal worked answers.
- Correct thermal models and calibration for all five chemicals; two actual guided trials, notebook means, saved observations, assessment quantity freedom and mistake feedback for each.
- Fusion-tube placement with its nested capillary, correct boiling observations, and visibility of the prepared fusion tube in all three boiling profiles.
- Calibration marks hidden in sample/thermometer close-ups and retained in immersion for all five profiles. Boiling Study and clear fusion views were visually reviewed.

The workflow suite used the final functional source. The following small calibration-overlay repair received the focused 18-check run; it does not alter heating or recorded data. Both runs compiled successfully. No new C# compilation, missing-script, null-reference or shader errors were found in these logs. The existing unrelated Unity Editor Search startup exception/renderer resource warning remains.

## Limits and re-run

No APK was rebuilt: the user explicitly requested source fixes and Editor review. Existing delivery artifacts predate this usability update. Physical Android touch comfort, keyboard, safe areas, lifecycle and measured 30 FPS remain for the user's later phone test.

Use **Naphthalene > Validate full experience** while out of Play for the current workflow suite. It creates isolated saves and writes UsabilityValidation.txt. The focused visibility entry point is NaphthaleneUsabilityValidation.CheckCloseups. Historical checkpoints are retained under Validation/Checkpoints.
