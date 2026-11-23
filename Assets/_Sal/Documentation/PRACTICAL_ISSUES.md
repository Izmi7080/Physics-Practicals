# Chemistry practical: outstanding issues and agreed changes

Recorded: 19 October 2026. Status: planning backlog; practical changes are deferred while a second practical is selected.

This records the student's reported experience and the agreed direction. A source-code check is not proof that a visual or touch issue is fixed. This documentation task includes no scene changes or APK build.

## 1. Reuse the existing main tap; remove the duplicate filling station

**Reported:** The laboratory already has a main sink and tap, but a second small sink/tap was added beside it.

**Confirmed cause:** `Assets/Editor/NaphthaleneTapLayout.cs` creates a separate `Tap station`, basin and `WaterTap` prefab instance instead of connecting the water interaction to the existing environment tap.

**Change:**

- Use the existing main bench tap and sink shown in the student's screenshot. Remove the added duplicate station after its functionality is transferred.
- Move the view smoothly to the main sink for filling. Keep the beaker and tap destination visible together.
- Rebind the tap control, water stream, beaker destination, empty/refill action, camera target and movement constraints to the original sink.
- Let the student fill against the beaker's marked level, stop the tap and return the beaker to the gauze.
- Preserve the refill action when moving the beaker between the sink and tripod; it must work in the bench and close-up views.
- Ensure the spout points into the basin and the stream starts at the actual outlet.

**Accept when:** Exactly one filling station is present; filling and refilling work at the original tap; the beaker can be returned without losing controls or preparation progress.

Screenshot reference: `C:/Users/saleha/AppData/Local/Temp/codex-clipboard-4d90a6b8-581a-47ef-8e8a-ecd6a0bda14b.png`.

## 2. Study and workbook backgrounds are difficult to read

**Reported:** Introduction/menu or pause text remains visible behind Study and workbook content.

**Change:** Use opaque reading surfaces and explicit page-layer visibility. Hide competing menu/pause content while a document is open. Preserve rounded glass styling for controls, not paragraph backgrounds. Keep consistent typography, spacing and selected tabs.

**Accept when:** Only the active document's text is visible; long reading sessions do not require interpreting overlapping layers.

## 3. Scrolling is broken and makes the procedure look incomplete

**Reported:** Study appears to show only five steps; Tasks and workbook also cannot be scrolled properly.

**Current state:** The melting definition has 12 steps. Study builds its procedure from the full step list; later entries are clipped below the viewport. The exact scrolling/input failure still needs reproduction.

**Change:** Fix drag and wheel scrolling, content sizing, clipping and pointer routing. Provide a visible scroll indicator. Make the last step, all observation fields, repeat action and final submission reachable at phone landscape sizes. Do not attribute the bug to paused time without testing it.

**Accept when:** Actual pointer gestures reach the bottom of Study, Tasks, every workbook page and Results while the experiment is paused.

## 4. Current instructions are too small and easy to miss

**Change:** Replace the weak top-corner presentation with one readable teaching board above/beside the working apparatus, taking inspiration from the reference video. Avoid a full-width blocking bar.

- Give the immediate action, what to look for, and a brief completion confirmation.
- Example: **Do:** Hold Grind. **Watch for:** white crystals becoming fine powder.
- Keep instructions linked to the current sub-action, not just the general procedure title.
- Put longer explanations in reopenable Study pages.
- Offer the relevant apparatus on the table as needed; do not restore the bottom inventory tray.

**Accept when:** A beginner can identify the next action without searching across several panels or opening Tasks after every operation.

## 5. Add automatic framing for steps that require a close-up

This supersedes the earlier optional-only camera preference.

- Keep source apparatus and destination visible when picking up and placing tools.
- Automatically frame the useful detail after placement: water reaching its line, grinding, capillary filling/packing, sample alignment, immersion and ignition.
- Show immersion near eye level with the water surface, bulb/sample and glass clearance visible.
- Never move the camera during a held drag or grinding gesture.
- Preserve zoom adjustment, smooth view joystick and an accessible Bench reset.
- Keep heating views stable; switching Sample/Thermometer must preserve heating and left-pointer stirring.

**Accept when:** Required apparatus and controls remain usable both zoomed and unzoomed; no tool needs a separate Find action; no scene/task is changed by inspection.

## 6. Packing needs clear visible compaction and clearer measurements

**Question:** Should a loose 10 mm column become shorter after tapping?

**Answer for the simulation:** Yes, air gaps should decrease while the amount of solid is conserved. The chosen school-method target is a 10 mm **packed** column, not an arbitrary loose height. Keep that method distinct from university micro-methods that use smaller columns.

**Current code:** It attempts to show loose height decreasing from roughly twice the packed height as packing progresses. The effect and the displayed quantity need visual verification and clearer teaching.

**Change:** Show particles settling, closing gaps and shortening the loose column. Label the final measurement as packed height. Allow repeated collection/packing until the guided target is met. Keep assessment freedom over the amount used and evaluate technique afterward.

**Accept when:** Packing visibly reduces loose volume without deleting sample; one dip cannot incorrectly finish an underfilled guided sample; assessment quantity is not silently replaced with the ideal amount.

## 7. Flame is offset beside the burner

**Reported:** The flame appears at the side rather than at the burner opening.

**Change:** Anchor the effect to a dedicated marker at the actual burner mouth. Recheck particle local position/orientation, heat-strength scaling, ignition and the capillary sealing target from several camera angles. Avoid relying on the combined renderer bounds as the mouth location.

**Accept when:** The flame starts centrally at the opening and rises upward in the bench and inspection views.

## 8. Explain and review the simplified melting model

**Current implementation:** The naphthalene profile specifies sample onset at 80 C and completion at 81 C. `MeltingSimulation.MeltFraction` linearly maps sample temperature between those limits. Bath, sample and thermometer have separate response lag; amount, packing, stirring and recording time affect the run. This is a deterministic teaching approximation, not a latent-heat solver.

**Change:**

- Explain the approximation in Study. Do not teach that every sample always finishes melting after exactly one degree.
- Make first wetting, partial melting and disappearance of the final solid clearly visible in the actual capillary.
- Keep sample appearance and captured sample condition driven by the same state.
- Review response calibration and technique effects without adding random errors or widening the range merely to make it look different.
- Keep the app's accelerated clock and scoring allowance separate from real laboratory rules.

**Accept when:** The student can recognise the two events visually; recorded temperatures are the values actually observed, not substituted example answers.

## 9. Guided observation fields need writing instructions and examples

**Change:** Put prompts directly beside each field, rather than relying on a distant reference page.

| Observation | Guided prompt/example |
| --- | --- |
| Initial appearance | Describe colour and physical form; example: white crystalline solid. |
| After grinding | Describe the smaller particle size and powder appearance. |
| First liquid | Describe a wet/clear liquid region and the solid grains still present. |
| Fully melted | Describe liquid appearance and whether any solid remains. |
| Temperature | Use the saved numeric temperature beside the immutable scale capture. |
| Reflection | Consider heating rate, stirring, packing and recording timing. |

Examples must be labelled and never inserted as the student's own answer. Assessment keeps understandable field labels but hides worked answers. Boiling needs bubble-stream observations instead of melting descriptions. Save drafts across pages and resume. Free-text answers receive guidance, not automated correctness grading.

## 10. Offer the second trial immediately and clearly

**Current issue:** The repeat action is buried down the workbook. After shutdown there is no clear invitation to begin Trial 2; broken scrolling makes the action harder to find.

**Change:** Show a Trial 1 complete summary immediately after shutdown, with saved readings and a prominent record/review-and-start-Trial-2 action. Explain the few trial-specific entries required before repeating. Preserve the first trial, cool/reset the relevant apparatus and offer a fresh prepared sample/capillary workflow. Leave combined calculations, final result and final conclusion until both trials are recorded.

**Accept when:** A beginner finds Trial 2 without searching a long form; two separate runs appear side by side; the within-trial melting interval is never presented as two trials.

## 11. Make the teaching experience visually consistent

- Use a small, consistent set of button sizes, rounded shapes, restrained glass highlights and clear selected states.
- Give controls deliberate spacing and meaningful apparatus-relative placement without covering drop targets.
- Keep navigation predictable; avoid a crowded corner, duplicate inspect controls and floating lists of apparatus views.
- Keep the Bench return comfortably inside the safe area.
- Keep small Move handles only where thin objects need them. Large objects and the pestle should remain directly draggable.
- Relevant handles follow their apparatus during movement; unrelated inspection controls should not wander or cover another task.
- Preserve simultaneous left-finger stirring and right-finger inspection/recording; pause/page opening/pointer loss stops tools safely.
- Keep live record controls near observations and readings/calculator together, following the reference video.

## 12. Regression checks from earlier feedback

These are checks to repeat, not claims that every old issue is still present:

- Sample remains visibly crystalline before grinding; loading alone does not turn it into powder.
- Pestle placement does not pass through the mortar, and the tip/sample remain visible while grinding.
- Capillary is visible, selectable and shows collected/packed white sample accurately.
- Sample aligns beside the thermometer bulb; thread stays above water and clear of the thermometer scale.
- Stirrer and beaker can be seen and reached together; no Find stirrer step.
- Fill/Refill labels match instructions; returning the beaker does not lose the refill action.
- Actual thermometer and sample inspections remain available; saved scale, numeric reading, sample condition and timestamp do not change after recording.
- Guided mode explains premature marks; assessment permits observation mistakes and reports them afterward.
- No Continue/Next pause is reintroduced between completed actions.
- Fusion tube/inverted capillary and electric heating remain present for the boiling profiles.
- Interrupted heating recovery, two-trial saving, 95 C shutdown, full restart and heating-only practice retry still follow the agreed rules.

## Delivery order when work resumes

1. Reuse the main sink/tap; repair reading layers and scrolling; expose Trial 2 clearly.
2. Improve the instruction board, guided writing prompts and automatic action framing.
3. Improve packing, flame alignment and observable melting; review thermal calibration.
4. Repeat complete guided/assessment and two-pointer workflows. Test scroll gestures, safe-area layout and Android touch behaviour on a real phone; target at least 30 FPS. Automated state tests alone do not verify these UX issues.

Keep one Main Camera, shared configurable chemical definitions, local saves, the reusable calculator and the existing original-project boundaries. Do not rebuild an APK unless requested.

## Second practical

The next practical will be chosen from Punjab Matric Physics/Chemistry after matching the notebook list against existing apparatus. See the FYP-level apparatus inventory and shortlist. No second-practical scene has been created or changed during this review.
