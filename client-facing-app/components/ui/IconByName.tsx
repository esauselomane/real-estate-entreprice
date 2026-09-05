import type { ComponentType, SVGProps } from "react";
import * as Lucide from "lucide-react";

const iconMap: Record<string, ComponentType<SVGProps<SVGSVGElement>>> = {
  Briefcase: Lucide.Briefcase,
  Building2: Lucide.Building2,
  FileText: Lucide.FileText,
  Image: Lucide.Image,
  Globe: Lucide.Globe,
  Shield: Lucide.Shield,
  UserCheck: Lucide.UserCheck,
  HeartHandshake: Lucide.HeartHandshake,
  Bot: Lucide.Bot,
  Sparkles: Lucide.Sparkles,
  Eye: Lucide.Eye,
  Mail: Lucide.Mail,
  Phone: Lucide.Phone,
  MapPin: Lucide.MapPin,
};

export default function IconByName({
  name,
  className,
}: {
  name: string;
  className?: string;
}) {
  const Icon = iconMap[name] ?? Lucide.Smile;
  return <Icon className={className ?? "h-5 w-5"} />;
}
