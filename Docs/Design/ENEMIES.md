# Enemy approach and pursuit

Ordinary enemies descend vertically at their birth x until their feet reach 1.7 world units above the hero's feet. This is an invisible activation region, not a stopping rail.

Once activated, enemies continuously seek the hero's current foot position along a direct line. Movement is speed limited and stops at enemy radius + hero ground radius (0.5) + 0.1. Local two-dimensional separation prevents stacking; enemies cannot cross through the hero or move behind the bottom play area. Moving the hero updates the pursuit target every combat step.

Enemies within a further 0.12 units begin a 0.3 second orange melee warning. A strike rechecks actual distance after separation and uses a 1.25 second cooldown. Leaving range cancels the windup; pursuit resumes on the next step. Pause, upgrades, death and recycling reset or stop the appropriate state.

No visible activation line is drawn. Ordinary soldiers retain the approved size and overhead red health bars.
