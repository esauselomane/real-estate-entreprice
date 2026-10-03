import { Mail, MapPin, Phone } from "lucide-react";
import ContactForm from "@/components/contact/ContactForm";

const contactDetails = {
  email: process.env.NEXT_PUBLIC_CONTACT_EMAIL || "admin@biscovetech.com",
  phone: "+27 75 0422 197",
  address: "8 Upper Mountain Road, Walmer Estate, Cape Town, 7925",
};

export const metadata = {
  title: "Contact",
  description:
    "Contact Biscove Technologies about our real estate and land administration solutions.",
};

export default function ContactPage() {
  return (
    <section className="py-10 sm:py-12">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-white sm:text-5xl">
            Contact us
          </h1>
          <p className="mt-3 text-lg text-slate-300">
            Tell us about your organisation and the solution you are interested in: real estate operations, land administration, or both.
          </p>
        </div>

        <div className="mt-8 grid gap-8 lg:grid-cols-2">
          <div className="space-y-4">
            <div className="flex items-start gap-3">
              <Mail className="mt-0.5 h-5 w-5 text-blue-400" />
              <div>
                <p className="font-semibold text-white">Email</p>
                <a
                  href={`mailto:${contactDetails.email}`}
                  className="text-slate-300 hover:text-blue-400"
                >
                  {contactDetails.email}
                </a>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <Phone className="mt-0.5 h-5 w-5 text-blue-400" />
              <div>
                <p className="font-semibold text-white">Phone</p>
                <p className="text-slate-300">{contactDetails.phone}</p>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <MapPin className="mt-0.5 h-5 w-5 text-blue-400" />
              <div>
                <p className="font-semibold text-white">Office</p>
                <p className="text-slate-300">{contactDetails.address}</p>
              </div>
            </div>
          </div>

          <ContactForm />
        </div>
      </div>
    </section>
  );
}
