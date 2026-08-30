import { Mail, MapPin, Phone } from "lucide-react";
import ContactForm from "@/components/contact/ContactForm";

const contactDetails = {
  email: process.env.NEXT_PUBLIC_CONTACT_EMAIL || "hello@realestate.example",
  phone: "+27 21 555 0199",
  address: "12th Floor, FoundryCourt, 16 Epping Crescent, Cape Town, South Africa",
};

export const metadata = {
  title: "Contact",
  description:
    "Reach out to Biscove Real Estate Technology — we'd love to hear from you.",
};

export default function ContactPage() {
  return (
    <section className="py-16 sm:py-20">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <h1 className="text-4xl font-display font-extrabold tracking-tight text-slate-900 sm:text-5xl">
            Contact us
          </h1>
          <p className="mt-4 text-lg text-slate-600">
            Have a question? Drop us a line and we&apos;ll get back to you shortly.
          </p>
        </div>

        <div className="mt-12 grid gap-8 lg:grid-cols-2">
          <div className="space-y-4">
            <div className="flex items-start gap-3">
              <Mail className="mt-0.5 h-5 w-5 text-blue-600" />
              <div>
                <p className="font-semibold text-slate-900">Email</p>
                <a
                  href={`mailto:${contactDetails.email}`}
                  className="text-slate-600 hover:text-blue-600"
                >
                  {contactDetails.email}
                </a>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <Phone className="mt-0.5 h-5 w-5 text-blue-600" />
              <div>
                <p className="font-semibold text-slate-900">Phone</p>
                <p className="text-slate-600">{contactDetails.phone}</p>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <MapPin className="mt-0.5 h-5 w-5 text-blue-600" />
              <div>
                <p className="font-semibold text-slate-900">Office</p>
                <p className="text-slate-600">{contactDetails.address}</p>
              </div>
            </div>
          </div>

          <ContactForm />
        </div>
      </div>
    </section>
  );
}
