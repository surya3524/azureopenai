# UI Settings Guide

## Adjusting AI Parameters

The UI now includes input boxes to control AI behavior per request.

### Location

The settings appear below the main text input area:

```
???????????????????????????????????????
? Enter a BMC job run                 ?
? ??????????????????????????????????? ?
? ? [Text Area]                     ? ?
? ??????????????????????????????????? ?
?                                     ?
? Temperature (0-2)  Max Output Tokens?
? [    0    ]        [   6000   ]     ?
?                                     ?
?                       [Send Button] ?
???????????????????????????????????????
```

### Settings Explained

#### Temperature
- **What it does**: Controls randomness/creativity
- **Range**: 0.0 to 2.0
- **Default**: 0
- **Recommendations**:
  - `0.0` - Deterministic (same input = same output)
  - `0.3` - Slight variation
  - `0.7` - Creative/varied responses
  - `1.0+` - Very creative/unpredictable

#### Max Output Tokens
- **What it does**: Limits response length
- **Range**: 100 to 128,000
- **Default**: 6000
- **Guidelines**:
  - `1000-2000` - Short, concise answers
  - `4000-6000` - Detailed analysis (recommended)
  - `8000+` - Very detailed, comprehensive

### How to Use

1. **Enter your BMC job log** in the text area
2. **Adjust settings** (or leave at defaults)
   - Change temperature for creativity
   - Change max tokens for response length
3. **Click Send**
4. Settings are shown in your message for reference

### Examples

#### Quick Error Check
```
Temperature: 0
Max Tokens: 2000
Use Case: Fast, concise analysis
Cost: Low
```

#### Standard Analysis (Default)
```
Temperature: 0
Max Tokens: 6000
Use Case: Detailed troubleshooting
Cost: Medium
```

#### Comprehensive Deep Dive
```
Temperature: 0
Max Tokens: 10000
Use Case: Complex issues needing full context
Cost: High
```

#### Creative Brainstorming
```
Temperature: 0.7
Max Tokens: 8000
Use Case: Multiple solution approaches
Cost: High
```

### Tips

? **DO**:
- Keep temperature at 0 for production analysis
- Use 4000-6000 tokens for most scenarios
- Experiment with settings in development

? **DON'T**:
- Set temperature > 1.0 for critical analysis
- Use max tokens < 1000 (responses may be cut off)
- Use max tokens > 10000 without reason (costs!)

### Settings Display

Your input message will show the settings used:

```
BMC job log (Your Input)
<your log text>

Settings: Temperature=0, MaxTokens=6000
```

This helps you track what settings produced each response.

### Persistence

- Settings **reset to defaults** on page refresh
- Each request is independent
- Settings are **not saved** between sessions

### Override Priority

The UI inputs **override** all other configuration:

1. ? **UI Input** (you set it)
2. Environment Variable
3. appsettings.json
4. Hardcoded defaults

This means you can temporarily override production settings for testing.
