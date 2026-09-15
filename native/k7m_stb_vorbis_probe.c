/* Decode an Ogg Vorbis file with RunningWild's stb_vorbis and print
 * sample rate, channels and duration. Used to prove a packaged .ogg is
 * what the game's decoder will actually play.
 *
 *   cl /nologo /O2 k7m_stb_vorbis_probe.c /I <runningwild>/frontend/sdl/third_party
 *
 * The implementation file stb_vorbis.c is compiled in as a translation unit
 * the same way music.c does (without STB_VORBIS_HEADER_ONLY).
 */
#include <stdio.h>
#include <stdlib.h>

#include "stb_vorbis.c"

int main(int argc, char **argv)
{
    int channels = 0;
    int sample_rate = 0;
    short *output = NULL;
    int samples;

    if (argc != 2) {
        fprintf(stderr, "usage: k7m_stb_vorbis_probe <file.ogg>\n");
        return 2;
    }

    samples = stb_vorbis_decode_filename(argv[1], &channels, &sample_rate, &output);
    if (samples < 0 || output == NULL) {
        fprintf(stderr, "FAILED stb_vorbis could not decode %s\n", argv[1]);
        return 1;
    }

    printf("OK rate=%d channels=%d samples=%d duration_s=%.3f\n",
           sample_rate, channels, samples,
           sample_rate > 0 ? (samples / (double)sample_rate) : 0.0);
    free(output);
    return 0;
}
