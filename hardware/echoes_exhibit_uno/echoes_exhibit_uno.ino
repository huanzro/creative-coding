#include <Adafruit_NeoPixel.h>
#include <Servo.h>
#include <stdlib.h>
#include <string.h>

// Exhibition configuration: two verified 15-pixel GRB strips.
const uint16_t PIXELS_A = 15;
const uint16_t PIXELS_B = 15;
Adafruit_NeoPixel lampA(PIXELS_A, 6, NEO_GRB + NEO_KHZ800);
Adafruit_NeoPixel lampB(PIXELS_B, 7, NEO_GRB + NEO_KHZ800);
Servo flower;
const uint8_t SERVO_PIN = 5;
const uint8_t LIMIT = 64;
const unsigned long WATCHDOG_MS = 800;

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
int lastAngle = -1;

void setLamp(Adafruit_NeoPixel &lamp, uint32_t &past, uint8_t r, uint8_t g, uint8_t b) {
  uint32_t next = lamp.Color(r, g, b);
  if (next == past) return;
  past = next;
  lamp.fill(next);
  lamp.show();
}

void setServo(int angle) {
  if (lastAngle == angle) return;
  lastAngle = angle;
  if (angle < 0) {
    if (flower.attached()) flower.detach();
    pinMode(SERVO_PIN, OUTPUT);
    digitalWrite(SERVO_PIN, LOW);
    return;
  }
  flower.write(angle);
  if (!flower.attached()) flower.attach(SERVO_PIN);
}

void stopOutputs() {
  setLamp(lampA, colorA, 0, 0, 0);
  setLamp(lampB, colorB, 0, 0, 0);
  setServo(-1);
  outputLive = false;
}

void readCommand(char *line, unsigned long now) {
  char *saved;
  char *word = strtok_r(line, " ", &saved);
  if (!word || strcmp(word, "EOL3") != 0) return;
  word = strtok_r(NULL, " ", &saved);
  if (!word || strcmp(word, "SET") != 0) return;
  long field[8];
  for (uint8_t i = 0; i < 8; ++i) {
    word = strtok_r(NULL, " ", &saved);
    if (!word || strlen(word) > 5) return;
    char *end;
    field[i] = strtol(word, &end, 10);
    if (end == word || *end != '\0') return;
  }
  if (strtok_r(NULL, " ", &saved) || field[0] < 0 || field[0] > 65535) return;
  for (uint8_t i = 1; i <= 6; ++i) if (field[i] < 0 || field[i] > LIMIT) return;
  if (field[7] != -1 && (field[7] < 60 || field[7] > 120)) return;
  setLamp(lampA, colorA, field[1], field[2], field[3]);
  setLamp(lampB, colorB, field[4], field[5], field[6]);
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
  }
}

void setup() {
  pinMode(SERVO_PIN, OUTPUT);
  digitalWrite(SERVO_PIN, LOW);
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
}
