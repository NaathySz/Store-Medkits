# [Store module] Medkits
Allows players to use customizable medkits that restore health, with configurable usage limits, costs, and healing mechanics per round.

# Config
Config will be auto generated. Default:
```json
{
  "amount_of_health": "++20", // Default for everyone, in case player doesnt have permissions inside "custom" 
  "max_use_per_round": 3,
  "credit_cost": 100,
  "min_hp": 50,
  "medkit_commands": [
    "medkit",
    "medic"
  ],
  "use_regen_timer": true, // Enables gradual health regeneration over time
  "regen_interval": 0.1, // Time between each health regeneration when the above is true
  "custom": {
    "@css/generic": {
      "health": "60", // Sets health to 60
      "max_uses": 3, // Max uses per round
      "credits": 200, // Credit cost
      "min_hp": 30 // Minimum health required to use
    },
    "@css/vip": {
      "health": "++90", // Increase current health by 90; if health is 5, it will become 95
      "max_uses": 1,
      "credits": 300,
      "min_hp": 10
    },
    "#admin": {
      "health": "++10",
      "max_uses": 5,
      "credits": 50,
      "min_hp": 70
    }
  },
  "ConfigVersion": 1
}
```
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/L4L611665R)
