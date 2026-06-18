#include <gp_XY.hxx>
#include "csharp_interop.h"

CSHARP_WRAPPER(gp_XY_New(gp_XY*& handle), {
                    handle = new gp_XY();
                    })

CSHARP_WRAPPER(gp_XY_New_double_boudle(const double theX, const double theY, gp_XY*& handle), {
                    handle = new gp_XY(theX, theY);
                    })

CSHARP_WRAPPER(gp_XY_SetCoord_int_double(gp_XY* self, const int theIndex, const double theXi), {
                    self->SetCoord(theIndex, theXi);
                    })

CSHARP_WRAPPER(gp_XY_Delete(const gp_XY* self), {
                    delete self;
                    })
