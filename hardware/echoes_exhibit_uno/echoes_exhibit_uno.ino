#include <Adafruit_NeoPixel.h>
#include <stdlib.h>
#include <string.h>
#include <util/atomic.h>
#if !defined(__AVR_ATmega328P__) || F_CPU != 16000000UL
#error "The combined exhibit firmware targets a 16 MHz ATmega328P Uno R3."
#endif

// Exhibition configuration: two verified 15-pixel GRB strips.
const uint16_t PIXELS_A = 15;
const uint16_t PIXELS_B = 15;
Adafruit_NeoPixel lampA(PIXELS_A, 6, NEO_GRB + NEO_KHZ800);
Adafruit_NeoPixel lampB(PIXELS_B, 7, NEO_GRB + NEO_KHZ800);
const uint8_t SERVO_PIN = 5;
const uint8_t FAN_PIN = 9;
const uint8_t FAN_LIMIT = 166;
const uint8_t LIMIT = 64;
const unsigned long WATCHDOG_MS = 800;
constexpr uint16_t FAN_TIMER_TOP = F_CPU / 25000UL - 1;
constexpr uint16_t SERVO_FRAME_TICKS = 500; // 20 ms at 40 us per Timer1 cycle.
volatile uint8_t servoPulseTicks = 0;
volatile uint16_t servoPhase = 0;

struct Input {
  uint8_t pin;
  bool enabled;
  bool activeHigh;
  unsigned int settleMs;
  bool reading;
  bool pressed;
  unsigned long changedAt;
};
// activeHigh=false enables the internal pull-up and detects a connection to GND.
// For a verified active-high push-pull module, set activeHigh=true.
Input inputs[] = {
  {2, true,  false, 45,  false, false, 0},
  {3, true,  false, 45,  false, false, 0},
  {4, true,  false, 110, false, false, 0},
};

char buffer[80];
uint8_t length = 0;
bool overflow = false;
bool outputLive = false;
unsigned long lastOutput = 0;
unsigned long lastReport = 0;
uint32_t colorA = 0, colorB = 0;
uint32_t targetA = 0, targetB = 0;

ISR(TIMER1_OVF_vect) {
  static uint8_t framePulse = 0;
  const uint16_t phase = servoPhase;
  if (phase == 0) {
    framePulse = servoPulseTicks;
    if (framePulse) PORTD |= _BV(PD5);
  }
  if (phase == framePulse) PORTD &= ~_BV(PD5);
  servoPhase = phase + 1 == SERVO_FRAME_TICKS ? 0 : phase + 1;
}

void setFan(uint8_t duty) {
  // The external open-collector stage on D9 inverts the signal.
  if (duty == 0) {
    TCCR1A = _BV(WGM11);
    digitalWrite(FAN_PIN, HIGH);
  } else {
    OCR1A = uint32_t(duty) * (FAN_TIMER_TOP + 1) / 255;
    TCCR1A = _BV(WGM11) | _BV(COM1A1) | _BV(COM1A0);
  }
}

void showLamp(Adafruit_NeoPixel &lamp, uint32_t &previous, uint32_t next) {
  if (previous == next || !lamp.canShow()) return;
  uint16_t phase;
  ATOMIC_BLOCK(ATOMIC_RESTORESTATE) { phase = servoPhase; }
  const uint16_t leadTicks = (uint32_t(lamp.numPixels()) * 30 + 199) / 40;
  // NeoPixel transmission masks interrupts. Send only during the servo's low interval.
  if (servoPulseTicks && (phase < 55 || phase + leadTicks >= SERVO_FRAME_TICKS)) return;
  previous = next;
  lamp.fill(next);
  lamp.show();
}

void setServo(int angle) {
  ATOMIC_BLOCK(ATOMIC_RESTORESTATE) {
    if (angle < 0) {
      TIMSK1 &= ~_BV(TOIE1);
      servoPulseTicks = 0;
      PORTD &= ~_BV(PD5);
    } else {
      if (!servoPulseTicks) {
        servoPhase = 0;
        TIFR1 = _BV(TOV1);
        TIMSK1 |= _BV(TOIE1);
      }
      servoPulseTicks = (544 + 1856UL * angle / 180 + 20) / 40;
    }
  }
}

void stopOutputs() {
  targetA = targetB = 0;
  setFan(0);
  setServo(-1);
  outputLive = false;
}

void readCommand(char *line, unsigned long now) {
  char *saved;
  char *word = strtok_r(line, " ", &saved);
  if (!word || strcmp(word, "EOL3") != 0) return;
  word = strtok_r(NULL, " ", &saved);
  if (!word) return;
  const bool withFan = strcmp(word, "IO") == 0;
  if (!withFan && strcmp(word, "SET") != 0) return;
  long field[9] = {};
  for (uint8_t i = 0; i < (withFan ? 9 : 8); ++i) {
    word = strtok_r(NULL, " ", &saved);
    if (!word || strlen(word) > 5) return;
    char *end;
    field[i] = strtol(word, &end, 10);
    if (end == word || *end != '\0') return;
  }
  if (strtok_r(NULL, " ", &saved) || field[0] < 0 || field[0] > 65535) return;
  for (uint8_t i = 1; i <= 6; ++i) if (field[i] < 0 || field[i] > LIMIT) return;
  if (field[7] != -1 && (field[7] < 60 || field[7] > 120)) return;
  if (field[8] < 0 || field[8] > FAN_LIMIT) return;
  targetA = lampA.Color(field[1], field[2], field[3]);
  targetB = lampB.Color(field[4], field[5], field[6]);
  setFan(field[8]);
  setServo(field[7]);
  outputLive = true;
  lastOutput = now;
  Serial.print(F("EOL3 ACK "));
  Serial.println(field[0]);
}

void scanInputs(unsigned long now) {
  uint8_t mask = 0, enabled = 0;
  bool changed = false;
  for (uint8_t i = 0; i < 3; ++i) {
    Input &input = inputs[i];
    if (!input.enabled) continue;
    enabled |= 1 << i;
    bool current = digitalRead(input.pin) == (input.activeHigh ? HIGH : LOW);
    if (input.reading != current) {
      input.reading = current;
      input.changedAt = now;
    }
    if (input.pressed != input.reading && now - input.changedAt >= input.settleMs) {
      input.pressed = input.reading;
      changed = true;
    }
    if (input.pressed) mask |= 1 << i;
  }
  if (changed || now - lastReport >= 250) {
    lastReport = now;
    Serial.print(F("EOL3 IN "));
    Serial.print(mask);
    Serial.print(' ');
    Serial.println(enabled);
    Serial.println(F("EOL3 CAP FAN"));
  }
}

void setup() {
  pinMode(SERVO_PIN, OUTPUT);
  digitalWrite(SERVO_PIN, LOW);
  digitalWrite(FAN_PIN, HIGH);
  pinMode(FAN_PIN, OUTPUT);
  TCCR1A = _BV(WGM11);
  TCCR1B = 0;
  TIMSK1 = 0;
  TCNT1 = 0;
  ICR1 = FAN_TIMER_TOP;
  TCCR1B = _BV(WGM13) | _BV(WGM12) | _BV(CS10);
  lampA.begin();
  lampB.begin();
  lampA.clear();
  lampB.clear();
  lampA.show();
  lampB.show();
  for (Input &input : inputs) {
    pinMode(input.pin, input.enabled && input.activeHigh ? INPUT : INPUT_PULLUP);
  }
  Serial.begin(115200);
}

void loop() {
  unsigned long now = millis();
  if (outputLive && now - lastOutput >= WATCHDOG_MS) stopOutputs();
  scanInputs(now);
  // Bound per-loop parsing so serial noise cannot monopolize the watchdog.
  for (uint8_t count = 0; count < 64 && Serial.available(); ++count) {
    char ch = Serial.read();
    if (ch == '\n') {
      if (!overflow) {
        buffer[length] = '\0';
        readCommand(buffer, now);
      }
      length = 0;
      overflow = false;
    } else if (ch != '\r' && !overflow) {
      if (length < sizeof(buffer) - 1) buffer[length++] = ch;
      else overflow = true;
    }
  }
  showLamp(lampA, colorA, targetA);
  showLamp(lampB, colorB, targetB);
}
