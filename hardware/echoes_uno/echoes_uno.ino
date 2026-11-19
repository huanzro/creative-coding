/* ARTISTIC RATIONALE
   Wind follows the journey; collected light appears in the room.
   Receiver for EOL2, UNO R3 / ATmega328P 16 MHz only.
   D9 -> external inverting open-collector interface -> 4-wire fan PWM.
   D6 -> 470 ohm -> WS2812B DIN. See ../../docs/HARDWARE.md before wiring.
   Requires Adafruit NeoPixel. No Servo library: Timer1 is reserved for 25 kHz.
   This example has not yet been compiled or tested on the physical installation.
*/
#include <Adafruit_NeoPixel.h>
#include <stdlib.h>
#include <string.h>
#if !defined(__AVR_ATmega328P__) || F_CPU != 16000000UL
#error "This receiver targets a 16 MHz ATmega328P (UNO R3)."
#endif

constexpr uint8_t FAN_PIN=9, LED_PIN=6, LED_COUNT=8;
constexpr uint8_t FAN_CAP=166, LIGHT_CAP=128;
Adafruit_NeoPixel pixels(LED_COUNT,LED_PIN,NEO_GRB+NEO_KHZ800);
char line[80];uint8_t used=0;bool overflow=false,live=false;
unsigned long lastPacket=0;uint16_t timeoutMs=800;

void fanDuty(uint8_t duty){
  duty=min(duty,FAN_CAP);
  // NPN / open-drain stage inverts D9: HIGH on D9 pulls the fan PWM LOW.
  if(duty==0){TCCR1A=_BV(WGM11);digitalWrite(FAN_PIN,HIGH);}
  else {OCR1A=uint16_t((uint32_t(duty)*640)/255);TCCR1A=_BV(WGM11)|_BV(COM1A1)|_BV(COM1A0);}
}
void allOff(){fanDuty(0);pixels.clear();pixels.show();live=false;}
bool number(char* token,unsigned long maximum,unsigned long &value){
  if(!token||!*token)return false;
  for(char* p=token;*p;p++)if(*p<'0'||*p>'9')return false;
  char* end;value=strtoul(token,&end,10);return *end=='\0'&&value<=maximum;
}
void receiveLine(){
  char* context;char* prefix=strtok_r(line," ",&context);char* command=strtok_r(nullptr," ",&context);
  if(!prefix||!command||strcmp(prefix,"EOL2")||strcmp(command,"SET"))return;
  unsigned long v[7];const unsigned long limits[]={65535,255,255,255,255,255,800};
  for(uint8_t i=0;i<7;i++)if(!number(strtok_r(nullptr," ",&context),limits[i],v[i]))return;
  if(strtok_r(nullptr," ",&context)||v[6]<100)return;
  fanDuty(uint8_t(v[1]));
  const uint8_t brightness=min(uint8_t(v[2]),LIGHT_CAP);
  const uint32_t color=pixels.Color(v[3]*brightness/255,v[4]*brightness/255,v[5]*brightness/255);
  for(uint8_t i=0;i<LED_COUNT;i++)pixels.setPixelColor(i,color);
  pixels.show();timeoutMs=uint16_t(v[6]);lastPacket=millis();live=true;
  // No continuous serial prints: browser currently provides write-only transport.
}
void setup(){
  digitalWrite(FAN_PIN,HIGH);pinMode(FAN_PIN,OUTPUT);
  TCCR1A=_BV(WGM11);TCCR1B=_BV(WGM13)|_BV(WGM12)|_BV(CS10);ICR1=639;TCNT1=0;
  pixels.begin();allOff();Serial.begin(115200);
}
void loop(){
  // Bounded processing so a noisy serial stream cannot starve the watchdog.
  for(uint8_t n=0;n<64&&Serial.available();n++){
    const char c=Serial.read();
    if(c=='\n'){if(!overflow){line[used]='\0';receiveLine();}used=0;overflow=false;}
    else if(c!='\r'){if(used<sizeof(line)-1&&!overflow)line[used++]=c;else overflow=true;}
  }
  if(live&&millis()-lastPacket>timeoutMs)allOff();
}
